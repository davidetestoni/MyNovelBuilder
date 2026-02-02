using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Dtos.Integrations;

/// <summary>
/// DTO for updating integrations configuration.
/// </summary>
public class UpdateIntegrationsConfigDto
{
    /// <summary>
    /// The OpenRouter API key.
    /// </summary>
    public string? OpenRouterApiKey { get; init; }
    
    /// <summary>
    /// The Google GenAI API key.
    /// </summary>
    public string? GoogleGenAiApiKey { get; init; }
    
    /// <summary>
    /// The ElevenLabs API key.
    /// </summary>
    public string? ElevenLabsApiKey { get; set; }
    
    /// <summary>
    /// The UnrealSpeech API key.
    /// </summary>
    public string? UnrealSpeechApiKey { get; set; }
    
    /// <summary>
    /// The Text Generation provider to use to generate text.
    /// </summary>
    public TextGenerationProvider? TextGenerationProvider { get; init; }
    
    /// <summary>
    /// The Text-to-Speech provider to use to generate speech.
    /// </summary>
    public TtsProvider? TtsProvider { get; init; }
    
    /// <summary>
    /// The TTS voice ID to use for text-to-speech generation.
    /// </summary>
    public string? TtsVoiceId { get; init; }
}
