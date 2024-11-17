namespace MyNovelBuilder.WebApi.Dtos.Generate;

/// <summary>
/// DTO for a Text-to-Speech request.
/// </summary>
public class TtsRequestDto
{
    /// <summary>
    /// The model ID to use for generating the audio, if required.
    /// </summary>
    public string? ModelId { get; set; }
    
    /// <summary>
    /// The voice ID to use for generating the audio.
    /// </summary>
    public required string VoiceId { get; set; }
    
    /// <summary>
    /// The message to generate audio for.
    /// </summary>
    public required string Message { get; set; }
}
