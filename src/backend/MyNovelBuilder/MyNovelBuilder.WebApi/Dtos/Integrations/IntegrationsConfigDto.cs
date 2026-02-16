using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Dtos.Integrations;

/// <summary>
/// DTO for reading integrations configuration.
/// Note: This DTO does not expose sensitive information such as API keys.
/// </summary>
public class IntegrationsConfigDto
{
    /// <summary>
    /// Indicates whether an OpenRouter API key is configured.
    /// </summary>
    public required bool HasOpenRouterApiKey { get; init; }
    
    /// <summary>
    /// Indicates whether a Google GenAI API key is configured.
    /// </summary>
    public required bool HasGoogleGenAiApiKey { get; init; }
    
    /// <summary>
    /// Indicates whether an ElevenLabs API key is configured.
    /// </summary>
    public required bool HasElevenLabsApiKey { get; init; }
    
    /// <summary>
    /// Indicates whether an UnrealSpeech API key is configured.
    /// </summary>
    public required bool HasUnrealSpeechApiKey { get; init; }

    /// <summary>
    /// Indicates whether a DeAPI API key is configured.
    /// </summary>
    public required bool HasDeApiApiKey { get; init; }
    
    /// <summary>
    /// The configured Text Generation provider.
    /// </summary>
    public required TextGenerationProvider TextGenerationProvider { get; init; }

    /// <summary>
    /// The configured Text-to-Speech provider.
    /// </summary>
    public required TtsProvider TtsProvider { get; init; }

    /// <summary>
    /// The configured Image Generation provider.
    /// </summary>
    public required ImageGenerationProvider ImageGenerationProvider { get; init; }
    
    /// <summary>
    /// The TTS voice ID to use for text-to-speech generation.
    /// </summary>
    public required string TtsVoiceId { get; init; } 
}
