using System.Security.Cryptography;
using System.Text;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Models.Integrations;

namespace MyNovelBuilder.WebApi.Helpers;

/// <summary>Hashes a configured TTS endpoint without exposing its URL.</summary>
public static class TtsEndpointIdentity
{
    /// <summary>Identifies the configured endpoint without exposing its URL.</summary>
    public static string? FromConfig(IntegrationsConfig config, TtsProvider provider)
    {
        var endpoint = GetConfiguredEndpoint(config, provider);
        // The configured URL can contain user info or a secret query parameter.
        return endpoint is null ? null : Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes($"tts-endpoint-v1\n{endpoint}")));
    }

    /// <summary>Resolves a local endpoint for in-memory execution only.</summary>
    public static Uri? GetBaseUri(IntegrationsConfig config, TtsProvider provider)
    {
        var endpoint = GetConfiguredEndpoint(config, provider);
        if (endpoint is null) return null;
        var fallback = GetConfiguredEndpoint(new IntegrationsConfig(), provider)!;
        return ProviderBaseUrlHelper.NormalizeHttpBaseUri(endpoint, fallback, provider.ToString());
    }

    private static string? GetConfiguredEndpoint(IntegrationsConfig config, TtsProvider provider)
    {
        return provider switch
        {
            TtsProvider.Custom => config.CustomTtsBaseUrl,
            TtsProvider.PocketTts => config.PocketTtsBaseUrl,
            TtsProvider.VibeVoice => config.VibeVoiceBaseUrl,
            TtsProvider.Chatterbox => config.ChatterboxBaseUrl,
            TtsProvider.Qwen3 => config.Qwen3BaseUrl,
            TtsProvider.OmniVoice => config.OmniVoiceBaseUrl,
            TtsProvider.Audio8 => config.Audio8BaseUrl,
            TtsProvider.KittenTts => config.KittenTtsBaseUrl,
            _ => null
        };
    }
}
