using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Dtos.Voice;

/// <summary>
/// Data transfer object for a voice.
/// </summary>
public class VoiceDto
{
    /// <summary>
    /// The voice's ID.
    /// </summary>
    public required Guid Id { get; set; }

    /// <summary>
    /// The date and time the voice was created.
    /// </summary>
    public required DateTime CreatedAt { get; set; }

    /// <summary>
    /// The date and time the voice was last updated.
    /// </summary>
    public required DateTime UpdatedAt { get; set; }

    /// <summary>
    /// The voice's name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The voice's gender.
    /// </summary>
    public required VoiceGender VoiceGender { get; set; }
}
