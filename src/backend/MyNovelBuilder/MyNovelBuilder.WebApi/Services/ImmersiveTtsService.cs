using System.Text.Json;
using System.Collections.Immutable;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Helpers;
using MyNovelBuilder.WebApi.Models.AudioGeneration;
using MyNovelBuilder.WebApi.Models.Prompts;
using MyNovelBuilder.WebApi.Models.Tts;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Plans and streams immersive multi-speaker TTS playback.
/// </summary>
public class ImmersiveTtsService : IImmersiveTtsService, IAudiobookImmersivePlanner
{
    private readonly AudiobookPreparationCache _annotationPreparations = new();
    private const int _outputSampleRate = 24000;
    private const short _outputChannels = 1;
    private const short _outputBitsPerSample = 16;
    private const int _maxResolvedChunkLength = 700;
    private const int _defaultPauseMs = 150;

    private readonly INovelPromptCreatorService _novelPromptCreatorService;
    private readonly ITextGenerationServiceResolver _textGenerationServiceResolver;
    private readonly IIntegrationsService _integrationsService;
    private readonly ICompendiumRecordService _compendiumRecordService;
    private readonly ITtsAudioGenerationService _ttsAudioGenerationService;
    private readonly ILogger<ImmersiveTtsService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ITtsVoiceRevisionService _voiceRevisions;

    /// <summary></summary>
    public ImmersiveTtsService(
        INovelPromptCreatorService novelPromptCreatorService,
        ITextGenerationServiceResolver textGenerationServiceResolver,
        IIntegrationsService integrationsService,
        ICompendiumRecordService compendiumRecordService,
        ITtsAudioGenerationService ttsAudioGenerationService,
        ILogger<ImmersiveTtsService> logger,
        IServiceProvider serviceProvider,
        ITtsVoiceRevisionService voiceRevisions)
    {
        _novelPromptCreatorService = novelPromptCreatorService;
        _textGenerationServiceResolver = textGenerationServiceResolver;
        _integrationsService = integrationsService;
        _compendiumRecordService = compendiumRecordService;
        _ttsAudioGenerationService = ttsAudioGenerationService;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _voiceRevisions = voiceRevisions;
    }

    /// <inheritdoc />
    public Task<ImmersiveTtsDebugResponseDto> PrepareDebugAsync(
        ImmersiveTtsRequestDto request,
        CancellationToken cancellationToken = default) => PrepareDebugCoreAsync(request, null, cancellationToken);

    /// <inheritdoc />
    public Task<ImmersiveTtsDebugResponseDto> PrepareDebugAsync(
        AudiobookRenderRequest request,
        CancellationToken cancellationToken = default) => PrepareDebugCoreAsync(ToPlaybackRequest(request), request, cancellationToken);

    private async Task<ImmersiveTtsDebugResponseDto> PrepareDebugCoreAsync(
        ImmersiveTtsRequestDto request,
        AudiobookRenderRequest? audiobook,
        CancellationToken cancellationToken)
    {
        var prepared = await PrepareAsync(request, audiobook, cancellationToken);
        return new ImmersiveTtsDebugResponseDto
        {
            AnnotationProfileId = TtsAnnotationProfile.Resolve(prepared.Provider, prepared.TtsModelId)?.Id,
            AnnotationProfileVersion = TtsAnnotationProfile.Version,
            Provider = prepared.Provider,
            TtsModelId = prepared.TtsModelId,
            TextGenerationModelId = prepared.TextGenerationModelId,
            PauseMs = prepared.PauseMs,
            Chunks = prepared.Chunks.Select((chunk, index) => new ImmersiveTtsDebugChunkDto
            {
                Sequence = index,
                SpeakerKind = chunk.SpeakerKind,
                SpeakerName = chunk.SpeakerName,
                CharacterRecordId = chunk.CharacterRecordId,
                VoiceId = chunk.VoiceId,
                IsNarratorFallback = chunk.IsNarratorFallback,
                Text = chunk.Text
            }).ToList()
        };
    }

    /// <inheritdoc />
    public Task<Stream> GenerateStreamAsync(
        ImmersiveTtsRequestDto request,
        CancellationToken cancellationToken = default) => GenerateStreamCoreAsync(request, null, cancellationToken);

    /// <inheritdoc />
    public Task<Stream> GenerateStreamAsync(
        AudiobookRenderRequest request,
        CancellationToken cancellationToken = default) => GenerateStreamCoreAsync(ToPlaybackRequest(request), request, cancellationToken);

    /// <inheritdoc />
    public async Task<ImmutableArray<AudiobookSpeechChunk>> PrepareAudiobookAsync(
        AudiobookRenderRequest request, CancellationToken cancellationToken = default)
    {
        var prepared = await PrepareAsync(ToPlaybackRequest(request), request, cancellationToken, preserveSpeechUnits: true);
        // The planner may redistribute whitespace, but must not omit, reorder or rewrite spoken characters.
        static string Spoken(string text) => string.Concat(text.Where(c => !char.IsWhiteSpace(c)));
        if (!TtsAnnotationService.PreservesSource(Spoken(request.Section.SpeechText), Spoken(string.Concat(prepared.Chunks.Select(c => c.Text)))))
            throw new InvalidDataException("Immersive planning changed or omitted source text. Retry planning before exporting.");
        return prepared.Chunks.Select(chunk => new AudiobookSpeechChunk(chunk.Text, chunk.VoiceId,
            request.Settings.ToTtsOptions(chunk.VoiceId, request.Section.VoiceAssignments
                .FirstOrDefault(a => a.CharacterRecordId == chunk.CharacterRecordId && a.VoiceId == chunk.VoiceId)?.VoiceRevision).VoiceRevision,
            chunk.IsNarratorFallback, chunk.GainDb)).ToImmutableArray();
    }

    private static ImmersiveTtsRequestDto ToPlaybackRequest(AudiobookRenderRequest request) => new()
    {
        NovelId = request.NovelId,
        ChapterIndex = request.ChapterIndex,
        SectionIndex = request.Section.SectionIndex,
        PromptId = request.Settings.ImmersivePromptId
            ?? throw new ArgumentException("Immersive rendering requires a planning prompt.", nameof(request))
    };

    private async Task<Stream> GenerateStreamCoreAsync(
        ImmersiveTtsRequestDto request,
        AudiobookRenderRequest? audiobook,
        CancellationToken cancellationToken)
    {
        var prepared = await PrepareAsync(request, audiobook, cancellationToken);
        _logger.LogInformation(
            "Starting immersive TTS stream for novel {NovelId}, chapter {ChapterIndex}, section {SectionIndex} with {ChunkCount} chunks.",
            request.NovelId,
            request.ChapterIndex,
            request.SectionIndex,
            prepared.Chunks.Count);

        return new PcmWavStreamingStream(
            sampleRate: _outputSampleRate,
            channels: _outputChannels,
            bitsPerSample: _outputBitsPerSample,
            producer: async (writeAsync, ct) =>
            {
                for (var i = 0; i < prepared.Chunks.Count; i++)
                {
                    var chunk = prepared.Chunks[i];
                    _logger.LogDebug(
                        "Synthesizing immersive TTS chunk {ChunkIndex}/{ChunkCount}: speakerKind={SpeakerKind}, speakerName={SpeakerName}, characterRecordId={CharacterRecordId}, voiceId={VoiceId}, textLength={TextLength}",
                        i + 1,
                        prepared.Chunks.Count,
                        chunk.SpeakerKind,
                        chunk.SpeakerName,
                        chunk.CharacterRecordId,
                        chunk.VoiceId,
                        chunk.Text.Length);
                    var wavBytes = await _ttsAudioGenerationService.GenerateWavBytesAsync(
                        new TextToSpeechGenerationRequest
                        {
                            Message = chunk.Text,
                            IsAnnotated = true,
                            Provider = prepared.Provider,
                            TtsModelId = prepared.TtsModelId,
                            VoiceId = chunk.VoiceId,
                            GainDb = chunk.GainDb,
                            TextGenerationModelId = prepared.TextGenerationModelId,
                            ResolvedOptions = audiobook?.Settings?.ToTtsOptions(
                                chunk.VoiceId,
                                audiobook?.Section?.VoiceAssignments
                                    .FirstOrDefault(assignment => assignment.CharacterRecordId == chunk.CharacterRecordId
                                        && assignment.VoiceId == chunk.VoiceId)?.VoiceRevision, chunk.GainDb)
                        },
                        ct);
                    _logger.LogDebug(
                        "Immersive TTS chunk {ChunkIndex}/{ChunkCount} synthesized successfully with {WavByteCount} WAV bytes.",
                        i + 1,
                        prepared.Chunks.Count,
                        wavBytes.Length);

                    var pcmBytes = await AudioConversionHelper.ConvertWavToPcmBytesAsync(
                        wavBytes,
                        _outputSampleRate,
                        _outputChannels,
                        _outputBitsPerSample,
                        ct);
                    _logger.LogDebug(
                        "Immersive TTS chunk {ChunkIndex}/{ChunkCount} converted to {PcmByteCount} PCM bytes.",
                        i + 1,
                        prepared.Chunks.Count,
                        pcmBytes.Length);
                    await writeAsync(pcmBytes);

                    if (prepared.PauseMs > 0 && i < prepared.Chunks.Count - 1)
                    {
                        _logger.LogDebug(
                            "Inserting immersive TTS pause of {PauseMs} ms after chunk {ChunkIndex}/{ChunkCount}.",
                            prepared.PauseMs,
                            i + 1,
                            prepared.Chunks.Count);
                        await writeAsync(AudioConversionHelper.CreateSilencePcm(
                            _outputSampleRate,
                            _outputChannels,
                            _outputBitsPerSample,
                            prepared.PauseMs));
                    }
                }
            },
            ct: cancellationToken);
    }

    private async Task<PreparedImmersiveTtsResult> PrepareAsync(
        ImmersiveTtsRequestDto request,
        AudiobookRenderRequest? audiobook,
        CancellationToken cancellationToken,
        bool preserveSpeechUnits = false)
    {
        var settings = audiobook?.Settings;
        var config = await _integrationsService.GetConfigAsync(cancellationToken);
        if (settings is not null && !string.Equals(
                settings.TtsEndpointIdentity,
                TtsEndpointIdentity.FromConfig(config, settings.Provider),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The TTS endpoint changed since the audiobook snapshot was created.");
        }
        if (settings is not null)
        {
            var narratorRevision = await _voiceRevisions.GetRevisionAsync(
                settings.Provider, settings.VoiceId, cancellationToken);
            if (narratorRevision != settings.NarratorVoiceRevision)
            {
                throw new InvalidOperationException("The narrator recording changed since the audiobook snapshot was created.");
            }
            if (audiobook?.Section is { } section)
            {
                foreach (var assignment in section.VoiceAssignments)
                {
                    var revision = await _voiceRevisions.GetRevisionAsync(
                        settings.Provider, assignment.VoiceId, cancellationToken);
                    if (revision != assignment.VoiceRevision)
                    {
                        throw new InvalidOperationException("A character recording changed since the audiobook snapshot was created.");
                    }
                }
            }
        }
        var provider = settings?.Provider ?? request.Provider ?? config.TtsProvider;
        var ttsModelId = settings?.ModelId ?? request.TtsModelId ?? config.TtsModelId;
        var narratorVoiceId = settings?.VoiceId ?? request.VoiceId ?? config.TtsVoiceId;
        var textGenerationModelId = settings?.TextGenerationModelId ?? request.TextGenerationModelId ?? config.TextGenerationModelId;
        var pauseMs = settings?.ImmersivePauseMs ?? request.PauseMs ?? config.TtsImmersivePauseMs;
        if (pauseMs < 0) pauseMs = _defaultPauseMs;
        _logger.LogDebug(
            "Preparing immersive TTS for novel {NovelId}, chapter {ChapterIndex}, section {SectionIndex} using textProvider={TextProvider}, textModel={TextModelId}, ttsProvider={TtsProvider}, ttsModel={TtsModelId}, narratorVoiceId={NarratorVoiceId}, pauseMs={PauseMs}.",
            request.NovelId,
            request.ChapterIndex,
            request.SectionIndex,
            settings?.TextGenerationProvider ?? config.TextGenerationProvider,
            textGenerationModelId,
            provider,
            ttsModelId,
            narratorVoiceId,
            pauseMs);

        var contextInfo = new PrepareImmersiveTtsContextInfoDto
        {
            NovelId = request.NovelId,
            ChapterIndex = request.ChapterIndex,
            SectionIndex = request.SectionIndex,
            SelectedText = request.SelectedText,
            Provider = provider,
            TtsModelId = ttsModelId
        };

        var promptRequest = new GenerateTextRequestDto
        {
            Model = textGenerationModelId,
            PromptId = request.PromptId,
            ContextInfo = contextInfo
        };

        var processedPrompt = audiobook?.Section is { } frozenSection
            ? new ProcessedPrompt
            {
                Messages = frozenSection.ImmersivePrompt.Select(message => new PromptMessage
                {
                    Role = message.Role,
                    Message = message.Message
                }).ToList(),
                IncludedCompendiumRecordIds = frozenSection.VoiceAssignments
                    .Select(assignment => assignment.CharacterRecordId).Distinct().ToList()
            }
            : await _novelPromptCreatorService.CreatePromptAsync(promptRequest, cancellationToken);
        var textGenerationService = settings is null
            ? await _textGenerationServiceResolver.GetConfiguredServiceAsync(cancellationToken)
            : _serviceProvider.GetRequiredKeyedService<TextGeneration.ITextGenerationService>(settings.TextGenerationProvider);
        var planningMessages = processedPrompt.Messages.Select(m => new PromptMessage { Role = m.Role, Message = m.Message }).ToList();
        string? planningText = null;
        var annotatedSources = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var message in planningMessages)
        {
            var begin = message.Message.IndexOf("<tts-source>", StringComparison.Ordinal);
            var end = message.Message.IndexOf("</tts-source>", StringComparison.Ordinal);
            if (begin < 0 || end < begin) continue;
            begin += "<tts-source>".Length;
            var source = message.Message[begin..end];
            var profile = (settings?.EnableTextEmphasis ?? config.TtsEnableTextEmphasis)
                ? TtsAnnotationProfile.Resolve(provider, ttsModelId) : null;
            if (!annotatedSources.TryGetValue(source, out planningText))
            {
                planningText = await _annotationPreparations.GetOrCreateAsync(audiobook?.Section,
                    AudioCacheKey.Create("section-annotation-v1", new { source, profile, textGenerationModelId,
                        textProvider = settings?.TextGenerationProvider ?? config.TextGenerationProvider }),
                    () => TtsAnnotationService.AnnotateAsync(source, profile,
                        textGenerationService, textGenerationModelId, cancellationToken, _logger));
                annotatedSources.Add(source, planningText);
            }
            message.Message = message.Message[..begin] + planningText + message.Message[end..];
        }
        if (request.SelectedText is not null && planningText is null)
            throw new InvalidDataException("The immersive prompt must include {{sectionText}} to narrate a selection.");
        if (planningText is null && (settings?.EnableTextEmphasis ?? config.TtsEnableTextEmphasis)
            && TtsAnnotationProfile.Resolve(provider, ttsModelId) is not null)
            throw new InvalidDataException("The immersive prompt must include {{sectionText}} to prepare annotations.");
        if (planningText is not null)
            planningMessages.Add(new PromptMessage { Role = PromptMessageRole.System,
                Message = "Partition only the tts-source passage. Copy all characters and delivery annotations in order. "
                    + "Preserve quotation marks (including opening and closing dialogue quotes), apostrophes, punctuation and language exactly. "
                    + "Do not paraphrase, correct spelling, normalize curly quotes, omit text, or add/remove delivery tags. "
                    + "Keep leading tags with their following speech and the correct character or narrator. "
                    + "Never create tag-only chunks or move directions between speakers. Keep each <whisper> scope balanced within one chunk." });
        var structuredChunks = await GenerateValidatedPlanAsync(textGenerationService, textGenerationModelId,
            planningMessages, contextInfo, planningText ?? audiobook?.Section.SpeechText, audiobook is not null, request, cancellationToken);
        var relevantRecords = audiobook?.Section is null && processedPrompt.IncludedCompendiumRecordIds.Any()
            ? await _compendiumRecordService.GetByIdsAsync(
                processedPrompt.IncludedCompendiumRecordIds,
                cancellationToken)
            : [];
        var recordsById = relevantRecords.ToDictionary(record => record.Id);
        var resolvedChunks = new List<ResolvedImmersiveTtsChunk>();

        foreach (var structuredChunk in structuredChunks)
        {
            var texts = preserveSpeechUnits
                ? new List<string> { structuredChunk.Text ?? string.Empty }
                : AudiobookTextSplitter.Split(structuredChunk.Text ?? string.Empty, _maxResolvedChunkLength, preserveTags: true).ToList();
            foreach (var textChunk in texts)
            {
                var resolvedChunk = ResolveChunk(
                    structuredChunk,
                    textChunk,
                    narratorVoiceId,
                    recordsById,
                    provider,
                    ttsModelId,
                    audiobook?.Section?.VoiceAssignments, settings?.GainDb ?? config.TtsGainDb);

                if (resolvedChunk is not null)
                {
                    resolvedChunks.Add(resolvedChunk);
                }
            }
        }

        if (resolvedChunks.Count == 0)
        {
            throw new ApiException(
                ErrorCodes.ExternalServiceError,
                "Immersive TTS planning produced no playable chunks.");
        }
        _logger.LogDebug(
            "Immersive TTS resolved to {ResolvedChunkCount} playable chunks: {@ResolvedChunks}",
            resolvedChunks.Count,
            resolvedChunks.Select(chunk => new
            {
                chunk.SpeakerKind,
                chunk.SpeakerName,
                chunk.CharacterRecordId,
                chunk.VoiceId,
                chunk.IsNarratorFallback,
                chunk.Text
            }).ToList());

        return new PreparedImmersiveTtsResult
        {
            Provider = provider,
            TtsModelId = ttsModelId,
            TextGenerationModelId = textGenerationModelId,
            PauseMs = pauseMs,
            Chunks = resolvedChunks
        };
    }

    private async Task<List<StructuredImmersiveTtsChunk>> GenerateValidatedPlanAsync(
        TextGeneration.ITextGenerationService generator, string model, List<PromptMessage> messages,
        PrepareImmersiveTtsContextInfoDto context, string? source, bool frozen,
        ImmersiveTtsRequestDto request, CancellationToken cancellationToken)
    {
        const int attempts = 3;
        var reason = "";
        string? previousPlan = null;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var attemptMessages = messages.Select(m => new PromptMessage { Role = m.Role, Message = m.Message }).ToList();
            if (previousPlan is not null)
            {
                attemptMessages.Add(new() { Role = PromptMessageRole.Assistant, Message = previousPlan });
                attemptMessages.Add(new() { Role = PromptMessageRole.User,
                    Message = "The previous plan failed validation: " + reason
                        + " Return a corrected complete JSON plan, not a patch. Copy the tts-source exactly, including all quotation marks, "
                        + "punctuation and delivery tags, in order. Do not add, delete or rewrite any prose or annotations. "
                        + "Only partition it and assign speakers. Each whisper scope must stay balanced in one chunk." });
            }
            var rawPlan = await generator.GenerateAsync(model, attemptMessages, context.GetStructuredOutputOptions(), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogDebug(
                "Immersive TTS planner attempt {Attempt} for novel {NovelId}, chapter {ChapterIndex}, section {SectionIndex}: {RawPlan}",
                attempt, request.NovelId, request.ChapterIndex, request.SectionIndex, rawPlan);
            var validation = ValidatePlan(rawPlan, source, frozen, out var repairDetails);
            if (validation.Error is null)
            {
                if (repairDetails is not null)
                    _logger.LogInformation(
                        "Restored immersive planner typography from source; novel {NovelId}, chapter {Chapter}, section {Section}, text model {ModelId}: {Difference}",
                        request.NovelId, request.ChapterIndex + 1, request.SectionIndex + 1, model, repairDetails);
                return validation.Chunks;
            }
            reason = validation.Error;
            _logger.LogWarning(
                "Immersive planning validation failed on attempt {Attempt}/{MaxAttempts}; novel {NovelId}, chapter {Chapter}, "
                + "section {Section}, text model {ModelId}: {Reason}",
                attempt, attempts, request.NovelId, request.ChapterIndex + 1, request.SectionIndex + 1, model, reason);
            previousPlan = rawPlan;
        }
        throw new ImmersivePlanningException(attempts, reason);
    }

    private static (List<StructuredImmersiveTtsChunk> Chunks, string? Error) ValidatePlan(string rawPlan, string? source, bool frozen, out string? repairDetails)
    {
        repairDetails = null;
        List<StructuredImmersiveTtsChunk> chunks;
        try { chunks = JsonSerializer.Deserialize<List<StructuredImmersiveTtsChunk>>(rawPlan, JsonDefaults.Options) ?? []; }
        catch (JsonException ex) { return ([], $"Invalid JSON at line {ex.LineNumber}, byte {ex.BytePositionInLine}."); }
        if (chunks.Count == 0) return (chunks, "The plan contains no chunks.");
        if (chunks.Any(c => c is null || c.Text is null)) return (chunks, "The plan contains a null chunk or null text.");
        if (frozen && chunks.Any(c =>
            !string.Equals(c.SpeakerKind, "narrator", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(c.SpeakerKind, "character", StringComparison.OrdinalIgnoreCase)))
            return (chunks, "A chunk has an invalid speakerKind; use narrator or character.");
        if (source is null) return (chunks, null);
        var expected = string.Concat(source.Where(c => !char.IsWhiteSpace(c)));
        var actual = string.Concat(string.Concat(chunks.Select(c => c.Text)).Where(c => !char.IsWhiteSpace(c)));
        if (expected != actual)
        {
            var difference = ImmersivePlanText.DescribeDifference(source, string.Concat(chunks.Select(c => c.Text)));
            if (!ImmersivePlanText.TryRestoreTypography(source, chunks.Select(c => c.Text).ToArray(), out var restored))
                return (chunks, difference);
            for (var index = 0; index < chunks.Count; index++) chunks[index].Text = restored[index];
            repairDetails = difference;
        }
        foreach (var chunk in chunks)
        {
            var scoped = false;
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(chunk.Text, "</?whisper>"))
            {
                var opening = match.Value == "<whisper>";
                if (opening == scoped) return (chunks, "A whisper scope crosses chunks or is unbalanced; keep its opening and closing tags in one chunk.");
                scoped = opening;
            }
            if (scoped) return (chunks, "A whisper scope crosses chunks or is unbalanced; keep its opening and closing tags in one chunk.");
        }
        return (chunks, null);
    }

    private static ResolvedImmersiveTtsChunk? ResolveChunk(
        StructuredImmersiveTtsChunk structuredChunk,
        string textChunk,
        string narratorVoiceId,
        IReadOnlyDictionary<Guid, CompendiumRecord> recordsById,
        TtsProvider provider,
        string ttsModelId,
        IReadOnlyList<AudiobookVoiceAssignment>? frozenAssignments, double narratorGainDb)
    {
        if (string.IsNullOrWhiteSpace(textChunk))
        {
            return null;
        }

        var isCharacter =
            string.Equals(structuredChunk.SpeakerKind, "character", StringComparison.OrdinalIgnoreCase);

        if (isCharacter && Guid.TryParse(structuredChunk.CharacterRecordId, out var recordId))
        {
            var record = recordsById.GetValueOrDefault(recordId);
            var voiceId = frozenAssignments is null
                ? record?.CharacterVoiceAssignments.FirstOrDefault(a =>
                    a.Provider == provider && string.Equals(a.ModelId, ttsModelId, StringComparison.Ordinal))?.VoiceId
                : frozenAssignments.FirstOrDefault(a => a.CharacterRecordId == recordId)?.VoiceId;

            if (voiceId is not null)
            {
                return new ResolvedImmersiveTtsChunk
                {
                    SpeakerKind = "character",
                    SpeakerName = string.IsNullOrWhiteSpace(structuredChunk.SpeakerName)
                        ? record?.Name ?? "Character"
                        : structuredChunk.SpeakerName,
                    CharacterRecordId = recordId,
                    VoiceId = voiceId,
                    GainDb = frozenAssignments is null
                        ? record!.CharacterVoiceAssignments.First(a => a.Provider == provider && a.ModelId == ttsModelId).GainDb
                        : frozenAssignments.First(a => a.CharacterRecordId == recordId).GainDb,
                    IsNarratorFallback = false,
                    Text = textChunk
                };
            }
        }

        return new ResolvedImmersiveTtsChunk
        {
            SpeakerKind = "narrator",
            SpeakerName = string.IsNullOrWhiteSpace(structuredChunk.SpeakerName)
                ? "Narrator"
                : structuredChunk.SpeakerName,
            CharacterRecordId = null,
            VoiceId = narratorVoiceId,
            GainDb = narratorGainDb,
            IsNarratorFallback = isCharacter,
            Text = textChunk
        };
    }

    private sealed class PreparedImmersiveTtsResult
    {
        public required TtsProvider Provider { get; init; }

        public required string TtsModelId { get; init; }

        public required string TextGenerationModelId { get; init; }

        public required int PauseMs { get; init; }

        public required List<ResolvedImmersiveTtsChunk> Chunks { get; init; }
    }

    private sealed class StructuredImmersiveTtsChunk
    {
        public string SpeakerKind { get; set; } = "narrator";

        public string SpeakerName { get; set; } = "Narrator";

        public string? CharacterRecordId { get; set; }

        public string Text { get; set; } = string.Empty;
    }

    private sealed class ResolvedImmersiveTtsChunk
    {
        public double GainDb { get; init; }
        public required string SpeakerKind { get; init; }

        public required string SpeakerName { get; init; }

        public required string VoiceId { get; init; }

        public required string Text { get; init; }

        public Guid? CharacterRecordId { get; init; }

        public bool IsNarratorFallback { get; init; }
    }
}
