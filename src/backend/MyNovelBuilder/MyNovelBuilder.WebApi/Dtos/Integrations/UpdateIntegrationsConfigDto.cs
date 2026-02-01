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
    public string? GoogleGenAiApiKey { get; set; }
    
    /// <summary>
    /// The Text Generation provider to use to generate text.
    /// </summary>
    public TextGenerationProvider? TextGenerationProvider { get; init; }
    
    /// <summary>
    /// The Text-to-Speech provider to use to generate speech.
    /// </summary>
    public TtsProvider? TtsProvider { get; init; }
}
