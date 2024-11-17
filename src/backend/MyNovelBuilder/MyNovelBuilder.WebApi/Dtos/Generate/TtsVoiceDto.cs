namespace MyNovelBuilder.WebApi.Dtos.Generate;

/// <summary>
/// DTO for a voice that is available for Text-to-Speech.
/// </summary>
public class TtsVoiceDto
{
    /// <summary>
    /// The ID of the voice.
    /// </summary>
    public required string VoiceId { get; set; }
    
    /// <summary>
    /// The name of the voice.
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// The URL to a preview of the voice.
    /// </summary>
    public required string PreviewUrl { get; set; }
}
