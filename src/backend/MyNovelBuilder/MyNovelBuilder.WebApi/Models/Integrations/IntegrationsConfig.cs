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
    /// The Text-to-Speech provider to use to generate speech.
    /// </summary>
    public TtsProvider TtsProvider { get; set; } = TtsProvider.ElevenLabs;
}
