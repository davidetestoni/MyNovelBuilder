using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Models.Tts;

/// <summary>
/// Internal request for generating TTS audio with resolved-or-overridden settings.
/// </summary>
public class TextToSpeechGenerationRequest
{
    /// <summary>
    /// The text to synthesize.
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Optional TTS provider override.
    /// </summary>
    public TtsProvider? Provider { get; set; }

    /// <summary>
    /// Optional TTS model override.
    /// </summary>
    public string? TtsModelId { get; set; }

    /// <summary>
    /// Optional voice override.
    /// </summary>
    public string? VoiceId { get; set; }

    /// <summary>
    /// Optional text-generation model override for preprocessing.
    /// </summary>
    public string? TextGenerationModelId { get; set; }
}
