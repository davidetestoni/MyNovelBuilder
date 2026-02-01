using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Models.Integrations;

/// <summary>
/// Configuration for integrations.
/// </summary>
public class IntegrationsConfig
{
    /// <summary>
    /// The OpenRouter API key.
    /// </summary>
    public string? OpenRouterApiKey { get; set; }
    
    /// <summary>
    /// The Google GenAI API key.
    /// </summary>
    public string? GoogleGenAiApiKey { get; set; }
    
    /// <summary>
    /// The Text Generation provider to use to generate text.
    /// </summary>
    public TextGenerationProvider TextGenerationProvider { get; set; } = TextGenerationProvider.OpenRouter;

    /// <summary>
    /// The Text-to-Speech provider to use to generate speech.
    /// </summary>
    public TtsProvider TtsProvider { get; set; } = TtsProvider.ElevenLabs;
    
    /// <summary>
    /// The TTS voice ID to use for text-to-speech generation.
    /// </summary>
    public string TtsVoiceId { get; set; } = string.Empty;
}
