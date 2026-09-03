namespace MyNovelBuilder.WebApi.Models.Tts;

/// <summary>
/// A Text-to-Speech request.
/// </summary>
public class TtsRequest
{
    /// <summary>
    /// The text generation model ID to use for auxiliary text-processing steps such as emphasis.
    /// </summary>
    public string? TextGenerationModelId { get; set; }

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
