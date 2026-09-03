using System.Text.Json;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Helpers;
using MyNovelBuilder.WebApi.Models.Prompts;
using MyNovelBuilder.WebApi.Models.Tts;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Plans and streams immersive multi-speaker TTS playback.
/// </summary>
public class ImmersiveTtsService : IImmersiveTtsService
{
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

    /// <summary></summary>
    public ImmersiveTtsService(
        INovelPromptCreatorService novelPromptCreatorService,
        ITextGenerationServiceResolver textGenerationServiceResolver,
        IIntegrationsService integrationsService,
        ICompendiumRecordService compendiumRecordService,
        ITtsAudioGenerationService ttsAudioGenerationService,
        ILogger<ImmersiveTtsService> logger)
    {
        _novelPromptCreatorService = novelPromptCreatorService;
        _textGenerationServiceResolver = textGenerationServiceResolver;
        _integrationsService = integrationsService;
        _compendiumRecordService = compendiumRecordService;
        _ttsAudioGenerationService = ttsAudioGenerationService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ImmersiveTtsDebugResponseDto> PrepareDebugAsync(
        ImmersiveTtsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var prepared = await PrepareAsync(request, cancellationToken);
        return new ImmersiveTtsDebugResponseDto
        {
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
    public async Task<Stream> GenerateStreamAsync(
        ImmersiveTtsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var prepared = await PrepareAsync(request, cancellationToken);
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
                            Provider = prepared.Provider,
                            TtsModelId = prepared.TtsModelId,
                            VoiceId = chunk.VoiceId,
                            TextGenerationModelId = prepared.TextGenerationModelId
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
        CancellationToken cancellationToken)
    {
        var config = await _integrationsService.GetConfigAsync(cancellationToken);
        var provider = request.Provider ?? config.TtsProvider;
        var ttsModelId = request.TtsModelId ?? config.TtsModelId;
        var narratorVoiceId = request.VoiceId ?? config.TtsVoiceId;
        var textGenerationModelId = request.TextGenerationModelId ?? config.TextGenerationModelId;
        var pauseMs = config.TtsImmersivePauseMs > 0
            ? config.TtsImmersivePauseMs
            : _defaultPauseMs;
        _logger.LogDebug(
            "Preparing immersive TTS for novel {NovelId}, chapter {ChapterIndex}, section {SectionIndex} using textProvider={TextProvider}, textModel={TextModelId}, ttsProvider={TtsProvider}, ttsModel={TtsModelId}, narratorVoiceId={NarratorVoiceId}, pauseMs={PauseMs}.",
            request.NovelId,
            request.ChapterIndex,
            request.SectionIndex,
            config.TextGenerationProvider,
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
            Provider = provider,
            TtsModelId = ttsModelId
        };

        var promptRequest = new GenerateTextRequestDto
        {
            Model = textGenerationModelId,
            PromptId = request.PromptId,
            ContextInfo = contextInfo
        };

        var processedPrompt = await _novelPromptCreatorService.CreatePromptAsync(
            promptRequest,
            cancellationToken);
        var textGenerationService =
            await _textGenerationServiceResolver.GetConfiguredServiceAsync(cancellationToken);
        var rawPlan = await textGenerationService.GenerateAsync(
            textGenerationModelId,
            processedPrompt.Messages,
            contextInfo.GetStructuredOutputOptions(),
            cancellationToken);
        _logger.LogDebug(
            "Immersive TTS planner returned raw plan for novel {NovelId}, chapter {ChapterIndex}, section {SectionIndex}: {RawPlan}",
            request.NovelId,
            request.ChapterIndex,
            request.SectionIndex,
            rawPlan);

        List<StructuredImmersiveTtsChunk> structuredChunks;
        try
        {
            structuredChunks = JsonSerializer.Deserialize<List<StructuredImmersiveTtsChunk>>(
                                   rawPlan,
                                   JsonDefaults.Options)
                               ?? [];
        }
        catch (JsonException ex)
        {
            throw new ApiException(
                ErrorCodes.ExternalServiceError,
                $"Immersive TTS planning returned invalid JSON: {ex.Message}");
        }

        if (structuredChunks.Count == 0)
        {
            throw new ApiException(
                ErrorCodes.ExternalServiceError,
                "Immersive TTS planning returned no chunks.");
        }
        _logger.LogDebug(
            "Immersive TTS planner produced {StructuredChunkCount} structured chunks.",
            structuredChunks.Count);
        var relevantRecords = processedPrompt.IncludedCompendiumRecordIds.Any()
            ? await _compendiumRecordService.GetByIdsAsync(
                processedPrompt.IncludedCompendiumRecordIds,
                cancellationToken)
            : [];
        var recordsById = relevantRecords.ToDictionary(record => record.Id);
        var resolvedChunks = new List<ResolvedImmersiveTtsChunk>();
        var chunker = new TextChunker(_maxResolvedChunkLength);

        foreach (var structuredChunk in structuredChunks)
        {
            foreach (var textChunk in chunker.ChunkText(structuredChunk.Text ?? string.Empty))
            {
                var resolvedChunk = ResolveChunk(
                    structuredChunk,
                    textChunk,
                    narratorVoiceId,
                    recordsById,
                    provider,
                    ttsModelId);

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

    private static ResolvedImmersiveTtsChunk? ResolveChunk(
        StructuredImmersiveTtsChunk structuredChunk,
        string textChunk,
        string narratorVoiceId,
        IReadOnlyDictionary<Guid, CompendiumRecord> recordsById,
        TtsProvider provider,
        string ttsModelId)
    {
        if (string.IsNullOrWhiteSpace(textChunk))
        {
            return null;
        }

        var isCharacter =
            string.Equals(structuredChunk.SpeakerKind, "character", StringComparison.OrdinalIgnoreCase);

        if (isCharacter
            && Guid.TryParse(structuredChunk.CharacterRecordId, out var recordId)
            && recordsById.TryGetValue(recordId, out var record))
        {
            var assignment = record.CharacterVoiceAssignments.FirstOrDefault(a =>
                a.Provider == provider
                && string.Equals(a.ModelId, ttsModelId, StringComparison.Ordinal));

            if (assignment is not null)
            {
                return new ResolvedImmersiveTtsChunk
                {
                    SpeakerKind = "character",
                    SpeakerName = string.IsNullOrWhiteSpace(structuredChunk.SpeakerName)
                        ? record.Name
                        : structuredChunk.SpeakerName,
                    CharacterRecordId = record.Id,
                    VoiceId = assignment.VoiceId,
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
        public required string SpeakerKind { get; init; }

        public required string SpeakerName { get; init; }

        public required string VoiceId { get; init; }

        public required string Text { get; init; }

        public Guid? CharacterRecordId { get; init; }

        public bool IsNarratorFallback { get; init; }
    }
}
