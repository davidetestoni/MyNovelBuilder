namespace MyNovelBuilder.WebApi.Models.Tts;

/// <summary>
/// A voice that is available for Text-to-Speech.
/// </summary>
public class TtsVoice
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
