using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Dtos.Generate;

/// <summary>
/// Debug response for immersive TTS chunk planning.
/// </summary>
public class ImmersiveTtsDebugResponseDto
{
    /// <summary>
    /// The resolved TTS provider.
    /// </summary>
    public required TtsProvider Provider { get; set; }

    /// <summary>
    /// The resolved TTS model.
    /// </summary>
    public required string TtsModelId { get; set; }

    /// <summary>
    /// The resolved text-generation model used for planning.
    /// </summary>
    public required string TextGenerationModelId { get; set; }

    /// <summary>
    /// The pause inserted between chunks.
    /// </summary>
    public required int PauseMs { get; set; }

    /// <summary>
    /// The resolved chunk plan.
    /// </summary>
    public required IEnumerable<ImmersiveTtsDebugChunkDto> Chunks { get; set; }
}

/// <summary>
/// A resolved immersive TTS chunk in debug form.
/// </summary>
public class ImmersiveTtsDebugChunkDto
{
    /// <summary>
    /// Zero-based chunk sequence number.
    /// </summary>
    public required int Sequence { get; set; }

    /// <summary>
    /// The speaker kind for this chunk.
    /// </summary>
    public required string SpeakerKind { get; set; }

    /// <summary>
    /// The resolved speaker display name.
    /// </summary>
    public required string SpeakerName { get; set; }

    /// <summary>
    /// The resolved character record ID, if any.
    /// </summary>
    public Guid? CharacterRecordId { get; set; }

    /// <summary>
    /// The resolved voice ID used for playback.
    /// </summary>
    public required string VoiceId { get; set; }

    /// <summary>
    /// Indicates whether the chunk fell back to the narrator voice.
    /// </summary>
    public bool IsNarratorFallback { get; set; }

    /// <summary>
    /// The text spoken in this chunk.
    /// </summary>
    public required string Text { get; set; }
}
