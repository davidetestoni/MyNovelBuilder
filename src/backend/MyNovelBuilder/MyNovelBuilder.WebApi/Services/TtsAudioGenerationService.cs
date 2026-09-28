using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Helpers;
using MyNovelBuilder.WebApi.Models.AudioGeneration;
using MyNovelBuilder.WebApi.Models.Tts;
using MyNovelBuilder.WebApi.Services.Tts;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Shared TTS generation pipeline for controller endpoints and immersive playback.
/// </summary>
public class TtsAudioGenerationService : ITtsAudioGenerationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IIntegrationsService _integrationsService;
    private readonly IAudioRepository _audioRepository;
    private readonly ITextGenerationServiceResolver _textGenerationServiceResolver;
    private readonly ILogger<TtsAudioGenerationService> _logger;
    private readonly ITtsVoiceRevisionService _voiceRevisions;

    /// <summary></summary>
    public TtsAudioGenerationService(
        IServiceProvider serviceProvider,
        IIntegrationsService integrationsService,
        IAudioRepository audioRepository,
        ITextGenerationServiceResolver textGenerationServiceResolver,
        ILogger<TtsAudioGenerationService> logger,
        ITtsVoiceRevisionService voiceRevisions)
    {
        _serviceProvider = serviceProvider;
        _integrationsService = integrationsService;
        _audioRepository = audioRepository;
        _textGenerationServiceResolver = textGenerationServiceResolver;
        _logger = logger;
        _voiceRevisions = voiceRevisions;
    }

    /// <inheritdoc />
    public async Task<byte[]> GenerateWavBytesAsync(
        TextToSpeechGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveRequestAsync(request, cancellationToken);
        // The playback cache omits endpoint and reference-recording revisions. Calls with
        // explicit resolved options use the versioned cache introduced in Step 2.
        var cachedAudioTask = request.ResolvedOptions is null
            ? _audioRepository.GetAudioFileAsync(resolved.AudioParameters, cancellationToken)
            : null;

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

        if (request.ResolvedOptions is null)
        {
            await _audioRepository.SaveAudioFileAsync(resolved.AudioParameters, wavBytes, cancellationToken);
        }
        return wavBytes;
    }

    /// <inheritdoc />
    public async Task<Stream> GenerateWavStreamAsync(
        TextToSpeechGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveRequestAsync(request, cancellationToken);
        var cachedAudioTask = request.ResolvedOptions is null
            ? _audioRepository.GetAudioFileAsync(resolved.AudioParameters, cancellationToken)
            : null;

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
            if (request.ResolvedOptions is null)
            {
                await _audioRepository.SaveAudioFileAsync(resolved.AudioParameters, wavBytes, cancellationToken);
            }
            return new MemoryStream(wavBytes);
        }

        if (request.ResolvedOptions is not null) return audioStream;

        return new CachingReadStream(
            audioStream,
            (audioData, ct) => _audioRepository.SaveAudioFileAsync(resolved.AudioParameters, audioData, ct),
            cancellationToken);
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
