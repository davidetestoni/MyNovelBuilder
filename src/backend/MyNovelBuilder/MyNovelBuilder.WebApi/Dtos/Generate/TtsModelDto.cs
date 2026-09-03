namespace MyNovelBuilder.WebApi.Dtos.Generate;

/// <summary>
/// DTO for a TTS model and its available voices.
/// </summary>
public class TtsModelDto
{
    /// <summary>
    /// The ID of the model.
    /// </summary>
    public required string ModelId { get; set; }

    /// <summary>
    /// The display name of the model.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Indicates whether this model supports text emphasis tags.
    /// </summary>
    public bool SupportsTextEmphasis { get; set; }

    /// <summary>
    /// The voices available for this model.
    /// </summary>
    public required IEnumerable<TtsVoiceDto> Voices { get; set; }
}
