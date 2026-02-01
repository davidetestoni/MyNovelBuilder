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
    public bool HasOpenRouterApiKey { get; init; }
    
    /// <summary>
    /// Indicates whether a Google GenAI API key is configured.
    /// </summary>
    public bool HasGoogleGenAiApiKey { get; init; }
    
    /// <summary>
    /// The configured Text Generation provider.
    /// </summary>
    public TextGenerationProvider TextGenerationProvider { get; init; }

    /// <summary>
    /// The configured Text-to-Speech provider.
    /// </summary>
    public TtsProvider TtsProvider { get; init; }
}
