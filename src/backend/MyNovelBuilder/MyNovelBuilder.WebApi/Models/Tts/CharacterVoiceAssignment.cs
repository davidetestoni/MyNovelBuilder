using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Models.Tts;

/// <summary>
/// A character-specific voice assignment for a provider/model combination.
/// </summary>
public class CharacterVoiceAssignment
{
    /// <summary>
    /// The TTS provider this assignment applies to.
    /// </summary>
    public required TtsProvider Provider { get; set; }

    /// <summary>
    /// The TTS model ID this assignment applies to.
    /// </summary>
    public required string ModelId { get; set; }

    /// <summary>
    /// The selected provider-specific voice ID.
    /// </summary>
    public required string VoiceId { get; set; }

    /// <summary>
    /// Optional voice display name snapshot for UI readability.
    /// </summary>
    public string? VoiceName { get; set; }

    /// <summary>
    /// The time the assignment was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
