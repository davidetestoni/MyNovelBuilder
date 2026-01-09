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
}
