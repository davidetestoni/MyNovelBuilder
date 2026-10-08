using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Helpers;
using MyNovelBuilder.WebApi.Models.AudioGeneration;
using MyNovelBuilder.WebApi.Models.Tts;
using MyNovelBuilder.WebApi.Services.Tts;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Shared TTS generation pipeline for controller endpoints and immersive playback.
/// </summary>
public class TtsAudioGenerationService : ITtsAudioGenerationService, IAudiobookTtsService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IIntegrationsService _integrationsService;
    private readonly IAudioRepository _audioRepository;
    private readonly ITextGenerationServiceResolver _textGenerationServiceResolver;
    private readonly ILogger<TtsAudioGenerationService> _logger;
    private readonly ITtsVoiceRevisionService _voiceRevisions;
    private readonly IAudioArtifactRepository _artifacts;
    private readonly AudioArtifactAssembler _assembler;
    private readonly AudiobookPreparationCache _preparations;

    /// <summary></summary>
    public TtsAudioGenerationService(
        IServiceProvider serviceProvider,
        IIntegrationsService integrationsService,
        IAudioRepository audioRepository,
        ITextGenerationServiceResolver textGenerationServiceResolver,
        ILogger<TtsAudioGenerationService> logger,
        ITtsVoiceRevisionService voiceRevisions,
        IAudioArtifactRepository artifacts,
        AudioArtifactAssembler assembler,
        AudiobookPreparationCache preparations)
    {
        _serviceProvider = serviceProvider;
        _integrationsService = integrationsService;
        _audioRepository = audioRepository;
        _textGenerationServiceResolver = textGenerationServiceResolver;
        _logger = logger;
        _voiceRevisions = voiceRevisions;
        _artifacts = artifacts;
        _assembler = assembler;
        _preparations = preparations;
    }

    /// <inheritdoc />
    public async Task<byte[]> GenerateWavBytesAsync(
        TextToSpeechGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveRequestAsync(request, cancellationToken);
        if (request.ResolvedOptions is not null)
            return await GenerateCachedAsync(resolved, cancellationToken);
        // Legacy playback entries never satisfy frozen audiobook requests.
        var cachedAudioTask = _audioRepository.GetAudioFileAsync(resolved.AudioParameters, cancellationToken);

        if (cachedAudioTask is not null)
        {
            var cachedAudioBytes = await cachedAudioTask;
            var normalizedCachedAudioBytes = await NormalizeCachedAudioBytesAsync(
                resolved.AudioParameters,
                cachedAudioBytes,
                cancellationToken);

            if (normalizedCachedAudioBytes is not null)
            {
                _logger.LogDebug("Using cached audio for textLength={TextLength}.", request.Message.Length);
                return normalizedCachedAudioBytes;
            }
        }

        var ttsRequest = await CreateTtsRequestAsync(resolved, cancellationToken);
        _logger.LogDebug(
            "Generating TTS audio bytes with provider={Provider}, modelId={ModelId}, voiceId={VoiceId}, textGenerationModelId={TextGenerationModelId}, textLength={TextLength}",
            resolved.AudioParameters.Provider,
            resolved.AudioParameters.ModelId,
            resolved.AudioParameters.VoiceId,
            resolved.AudioParameters.TextGenerationModelId,
            ttsRequest.Message.Length);
        var ttsResponse = await resolved.TtsService.GenerateAudioAsync(ttsRequest, cancellationToken);
        var wavBytes = resolved.TtsService.OutputAudioFormat == AudioFormat.Mp3
            ? await AudioConversionHelper.ConvertMp3ToWavBytesAsync(ttsResponse, cancellationToken)
            : ttsResponse;
        _logger.LogDebug(
            "TTS audio bytes generated successfully with {WavByteCount} WAV bytes.",
            wavBytes.Length);

        await _audioRepository.SaveAudioFileAsync(resolved.AudioParameters, wavBytes, cancellationToken);
        return wavBytes;
    }

    /// <inheritdoc />
    public async Task<Stream> GenerateWavStreamAsync(
        TextToSpeechGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveRequestAsync(request, cancellationToken);
        if (request.ResolvedOptions is not null)
        {
            var key = await EnsureArtifactAsync(resolved, cancellationToken);
            return await _artifacts.OpenReadAsync(key, cancellationToken)
                ?? throw new IOException("Generated audio disappeared before it could be opened.");
        }
        var cachedAudioTask = _audioRepository.GetAudioFileAsync(resolved.AudioParameters, cancellationToken);

        if (cachedAudioTask is not null)
        {
            var cachedAudioBytes = await cachedAudioTask;
            var normalizedCachedAudioBytes = await NormalizeCachedAudioBytesAsync(
                resolved.AudioParameters,
                cachedAudioBytes,
                cancellationToken);

            if (normalizedCachedAudioBytes is not null)
            {
                _logger.LogDebug("Using cached audio stream for textLength={TextLength}.", request.Message.Length);
                return new MemoryStream(normalizedCachedAudioBytes);
            }
        }

        var ttsRequest = await CreateTtsRequestAsync(resolved, cancellationToken);
        Stream audioStream;

        try
        {
            audioStream = await resolved.TtsService.GenerateAudioStreamAsync(ttsRequest, cancellationToken);
        }
        catch (NotImplementedException)
        {
            var audioBytes = await resolved.TtsService.GenerateAudioAsync(ttsRequest, cancellationToken);
            audioStream = new MemoryStream(audioBytes);
        }

        if (resolved.TtsService.OutputAudioFormat == AudioFormat.Mp3)
        {
            var originalStream = audioStream;
            await using (originalStream)
            {
                audioStream = await AudioConversionHelper.ConvertMp3ToWavStreamAsync(
                    originalStream,
                    cancellationToken);
            }

            await using var wavBuffer = new MemoryStream();
            await audioStream.CopyToAsync(wavBuffer, cancellationToken);
            var wavBytes = wavBuffer.ToArray();
            await _audioRepository.SaveAudioFileAsync(resolved.AudioParameters, wavBytes, cancellationToken);
            return new MemoryStream(wavBytes);
        }

        return new CachingReadStream(
            audioStream,
            (audioData, ct) => _audioRepository.SaveAudioFileAsync(resolved.AudioParameters, audioData, ct),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> GenerateArtifactAsync(TextToSpeechGenerationRequest request, CancellationToken cancellationToken = default)
    {
        var options = request.ResolvedOptions ?? throw new ArgumentException("Audiobook synthesis requires frozen settings.", nameof(request));
        var scope = request.PreparationScope ?? new AudiobookSectionSnapshot(0, request.Message, [], []);
        var keys = new List<string>();
        foreach (var text in AudiobookTextSplitter.Split(request.Message, AudiobookTextSplitter.Limit(options.Provider)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(text)) continue;
            var chunkRequest = new TextToSpeechGenerationRequest
            {
                Message = text, ResolvedOptions = options, PreparationScope = scope
            };
            keys.Add(await EnsureArtifactAsync(await ResolveRequestAsync(chunkRequest, cancellationToken), cancellationToken));
        }
        if (keys.Count == 0) throw new InvalidOperationException("There is no spoken text to synthesize.");
        return keys.Count == 1 ? keys[0] : await _assembler.AssembleAsync(keys, 0, cancellationToken);
    }

    private async Task<byte[]> GenerateCachedAsync(ResolvedTtsRequest resolved, CancellationToken cancellationToken)
    {
        var key = await EnsureArtifactAsync(resolved, cancellationToken);
        return await _artifacts.ReadBytesAsync(key, cancellationToken)
            ?? throw new IOException("Generated audio disappeared before it could be read.");
    }

    private async Task<string> EnsureArtifactAsync(ResolvedTtsRequest resolved, CancellationToken cancellationToken)
    {
        var options = resolved.Request.ResolvedOptions!;
        var emphasized = resolved.EnableTextEmphasis && resolved.TtsService.SupportsTextEmphasis(options.ModelId);
        var preparationKey = emphasized
            ? AudioCacheKey.Emphasis(resolved.Request.Message, options,
                AudioCacheKey.Create("emphasis-messages-v1", resolved.TtsService.GetEmphasisMessages(resolved.Request.Message)))
            : null;
        var sourceKey = AudioCacheKey.Source(resolved.Request.Message, options, preparationKey);
        if (resolved.Request.PreparationScope is not null)
            sourceKey = AudioCacheKey.Create("finite-tts-v1", new { sourceKey, splitter = AudiobookTextSplitter.Version });
        using var sourceLease = await _artifacts.AcquireAsync(sourceKey, cancellationToken);
        var manifest = await _artifacts.ReadManifestAsync(sourceKey, cancellationToken);
        if (manifest is { ArtifactKeys.Length: 1 }) return manifest.ArtifactKeys[0];

        var ttsRequest = await _preparations.GetOrCreateAsync(resolved.Request.PreparationScope,
            "emphasis-" + (preparationKey ?? AudioCacheKey.Hash(resolved.Request.Message)),
            async () => (await CreateTtsRequestAsync(resolved, cancellationToken)).Message);
        // Keep only prepared text across attempts. Execution inputs are validated afresh per call.
        var keys = new List<string>();
        var speechParts = resolved.Request.PreparationScope is null
            ? new[] { ttsRequest }
            : AudiobookTextSplitter.Split(ttsRequest, AudiobookTextSplitter.Limit(options.Provider), preserveTags: emphasized);
        foreach (var text in speechParts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(text)) continue;
            var chunkKey = AudioCacheKey.Synthesis(text, options);
            using var artifactLease = await _artifacts.AcquireAsync(chunkKey, cancellationToken);
            await using var cached = await _artifacts.OpenReadAsync(chunkKey, cancellationToken);
            if (cached is null)
            {
                var chunkRequest = new TtsRequest
                {
                    Message = text, VoiceId = options.VoiceId, ModelId = options.ModelId,
                    TextGenerationModelId = options.TextGenerationModelId, ExecutionInputs = resolved.ExecutionInputs
                };
                var audio = await resolved.TtsService.GenerateAudioAsync(chunkRequest, cancellationToken);
                if (resolved.TtsService.OutputAudioFormat == AudioFormat.Mp3)
                    audio = await AudioConversionHelper.ConvertMp3ToWavBytesAsync(audio, cancellationToken);
                await using var buffer = new MemoryStream(audio, writable: false);
                await _artifacts.SaveAsync(chunkKey, buffer, cancellationToken);
            }
            keys.Add(chunkKey);
        }
        if (keys.Count == 0) throw new InvalidDataException("Emphasis returned no spoken text.");
        var artifactKey = keys.Count == 1 ? keys[0] : await _assembler.AssembleAsync(keys, 0, cancellationToken);
        await _artifacts.SaveManifestAsync(new AudioSourceManifest(1, sourceKey, preparationKey, [artifactKey]), cancellationToken);
        return artifactKey;
    }

    private async Task<byte[]?> NormalizeCachedAudioBytesAsync(
        AudioGenerationParameters audioParameters,
        byte[] cachedAudioBytes,
        CancellationToken cancellationToken)
    {
        if (AudioConversionHelper.IsValidWav(cachedAudioBytes))
        {
            return cachedAudioBytes;
        }

        if (AudioConversionHelper.LooksLikeWav(cachedAudioBytes))
        {
            _logger.LogWarning(
                "Cached audio looked like WAV but failed validation for provider={Provider}, modelId={ModelId}, voiceId={VoiceId}. Ignoring cache entry and regenerating.",
                audioParameters.Provider,
                audioParameters.ModelId,
                audioParameters.VoiceId);
            return null;
        }

        if (AudioConversionHelper.LooksLikeMp3(cachedAudioBytes))
        {
            _logger.LogWarning(
                "Cached audio was MP3 instead of WAV for provider={Provider}, modelId={ModelId}, voiceId={VoiceId}. Converting and refreshing cache.",
                audioParameters.Provider,
                audioParameters.ModelId,
                audioParameters.VoiceId);
            var wavBytes = await AudioConversionHelper.ConvertMp3ToWavBytesAsync(
                cachedAudioBytes,
                cancellationToken);
            await _audioRepository.SaveAudioFileAsync(audioParameters, wavBytes, cancellationToken);
            return wavBytes;
        }

        _logger.LogWarning(
            "Cached audio was neither WAV nor MP3 for provider={Provider}, modelId={ModelId}, voiceId={VoiceId}. Ignoring cache entry and regenerating.",
            audioParameters.Provider,
            audioParameters.ModelId,
            audioParameters.VoiceId);
        return null;
    }

    private async Task<ResolvedTtsRequest> ResolveRequestAsync(
        TextToSpeechGenerationRequest request,
        CancellationToken cancellationToken)
    {
        var config = await _integrationsService.GetConfigAsync(cancellationToken);
        var options = request.ResolvedOptions;
        if (options is not null && !string.Equals(
                options.EndpointIdentity,
                TtsEndpointIdentity.FromConfig(config, options.Provider),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The TTS endpoint changed since the audiobook snapshot was created.");
        }
        var effectiveProvider = options?.Provider ?? request.Provider ?? config.TtsProvider;
        var effectiveModelId = options?.ModelId ?? request.TtsModelId ?? config.TtsModelId;
        var effectiveVoiceId = options?.VoiceId ?? request.VoiceId ?? config.TtsVoiceId;
        TtsExecutionInputs? executionInputs = null;
        if (options is not null)
        {
            var baseUri = TtsEndpointIdentity.GetBaseUri(config, effectiveProvider);
            var voice = await _voiceRevisions.CaptureAsync(
                effectiveProvider, effectiveVoiceId, cancellationToken);
            if (!string.Equals(options.VoiceRevision, voice?.Revision, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The recorded voice changed since the audiobook snapshot was created.");
            }
            executionInputs = new TtsExecutionInputs(baseUri, voice);
        }
        var effectiveTextGenerationModelId = options?.TextGenerationModelId ?? request.TextGenerationModelId ?? config.TextGenerationModelId;
        var enableTextEmphasis = options?.EnableTextEmphasis ?? config.TtsEnableTextEmphasis;
        var ttsService = _serviceProvider.GetKeyedService<ITtsService>(effectiveProvider);

        if (ttsService is null)
        {
            _logger.LogError("Unsupported TTS provider: {Provider}", effectiveProvider);
            throw new InvalidOperationException($"Unsupported TTS provider: {effectiveProvider}");
        }

        _logger.LogDebug(
            "Resolved TTS request: provider={Provider}, modelId={ModelId}, voiceId={VoiceId}, textGenerationModelId={TextGenerationModelId}, enableTextEmphasis={EnableTextEmphasis}, textLength={TextLength}.",
            effectiveProvider,
            effectiveModelId,
            effectiveVoiceId,
            effectiveTextGenerationModelId,
            enableTextEmphasis,
            request.Message.Length);

        return new ResolvedTtsRequest
        {
            Request = request,
            ExecutionInputs = executionInputs,
            TtsService = ttsService,
            AudioParameters = new AudioGenerationParameters
            {
                Text = request.Message,
                Provider = effectiveProvider,
                ModelId = effectiveModelId,
                VoiceId = effectiveVoiceId,
                TextGenerationModelId = effectiveTextGenerationModelId,
                EnableTextEmphasis = enableTextEmphasis
            },
            EnableTextEmphasis = enableTextEmphasis,
            TextGenerationProvider = options?.TextGenerationProvider,
            EffectiveTextGenerationModelId = effectiveTextGenerationModelId,
            EffectiveTtsModelId = effectiveModelId,
            EffectiveVoiceId = effectiveVoiceId
        };
    }

    private async Task<TtsRequest> CreateTtsRequestAsync(
        ResolvedTtsRequest resolved,
        CancellationToken cancellationToken)
    {
        var ttsRequest = new TtsRequest
        {
            Message = resolved.Request.Message,
            ExecutionInputs = resolved.ExecutionInputs,
            ModelId = resolved.EffectiveTtsModelId,
            VoiceId = resolved.EffectiveVoiceId,
            TextGenerationModelId = resolved.EffectiveTextGenerationModelId
        };

        if (!resolved.EnableTextEmphasis
            || !resolved.TtsService.SupportsTextEmphasis(resolved.EffectiveTtsModelId))
        {
            return ttsRequest;
        }

        var emphasizedText = await resolved.TtsService.EmphasizeTextAsync(
            ttsRequest,
            resolved.TextGenerationProvider is { } provider
                ? ct => ValueTask.FromResult(_serviceProvider.GetRequiredKeyedService<TextGeneration.ITextGenerationService>(provider))
                : _textGenerationServiceResolver.GetConfiguredServiceAsync,
            cancellationToken);
        ttsRequest.Message = emphasizedText.Trim();

        if (!string.Equals(ttsRequest.Message, resolved.Request.Message, StringComparison.Ordinal))
        {
            _logger.LogDebug("Emphasized text: {EmphasizedText}", ttsRequest.Message);
        }

        return ttsRequest;
    }

    private sealed class ResolvedTtsRequest
    {
        public required TextToSpeechGenerationRequest Request { get; init; }

        public required ITtsService TtsService { get; init; }

        public required AudioGenerationParameters AudioParameters { get; init; }

        public TtsExecutionInputs? ExecutionInputs { get; init; }

        public required bool EnableTextEmphasis { get; init; }

        public TextGenerationProvider? TextGenerationProvider { get; init; }

        public required string EffectiveTtsModelId { get; init; }

        public required string EffectiveVoiceId { get; init; }

        public required string EffectiveTextGenerationModelId { get; init; }
    }
}
