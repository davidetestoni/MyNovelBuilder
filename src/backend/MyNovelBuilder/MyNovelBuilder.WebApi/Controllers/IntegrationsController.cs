using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Hybrid;
using MyNovelBuilder.WebApi.Dtos.Integrations;
using MyNovelBuilder.WebApi.Models.Integrations;
using MyNovelBuilder.WebApi.Services;

namespace MyNovelBuilder.WebApi.Controllers;

/// <summary>
/// Controller for integrations.
/// </summary>
[Route("api/integrations")]
[ApiController]
public class IntegrationsController : ControllerBase
{
    private readonly IIntegrationsService _integrationsService;
    private readonly ILogger<IntegrationsController> _logger;
    private readonly HybridCache _hybridCache;

    /// <summary></summary>
    public IntegrationsController(
        IIntegrationsService integrationsService,
        ILogger<IntegrationsController> logger,
        HybridCache hybridCache)
    {
        _integrationsService = integrationsService;
        _logger = logger;
        _hybridCache = hybridCache;
    }
    
    /// <summary>
    /// Get the integrations' configuration.
    /// </summary>
    [HttpGet("config")]
    public async Task<IntegrationsConfigDto> GetIntegrationsConfig(CancellationToken cancellationToken = default)
    {
        var config = await _integrationsService.GetConfigAsync(cancellationToken);
        return new IntegrationsConfigDto
        {
            HasOpenRouterApiKey = !string.IsNullOrWhiteSpace(config.OpenRouterApiKey),
            HasGoogleGenAiApiKey = !string.IsNullOrWhiteSpace(config.GoogleGenAiApiKey),
            HasElevenLabsApiKey = !string.IsNullOrWhiteSpace(config.ElevenLabsApiKey),
            HasUnrealSpeechApiKey = !string.IsNullOrWhiteSpace(config.UnrealSpeechApiKey),
            HasDeApiApiKey = !string.IsNullOrWhiteSpace(config.DeApiApiKey),
            HasNanoGptApiKey = !string.IsNullOrWhiteSpace(config.NanoGptApiKey),
            CustomTtsBaseUrl = config.CustomTtsBaseUrl,
            PocketTtsBaseUrl = config.PocketTtsBaseUrl,
            VibeVoiceBaseUrl = config.VibeVoiceBaseUrl,
            ChatterboxBaseUrl = config.ChatterboxBaseUrl,
            Qwen3BaseUrl = config.Qwen3BaseUrl,
            OmniVoiceBaseUrl = config.OmniVoiceBaseUrl,
            TextGenerationProvider = config.TextGenerationProvider,
            TextGenerationModelId = config.TextGenerationModelId,
            TtsProvider = config.TtsProvider,
            ImageGenerationProvider = config.ImageGenerationProvider,
            VideoGenerationProvider = config.VideoGenerationProvider,
            TtsModelId = config.TtsModelId,
            TtsVoiceId = config.TtsVoiceId,
            TtsEnableTextEmphasis = config.TtsEnableTextEmphasis,
            TtsEnableImmersive = config.TtsEnableImmersive,
            TtsImmersivePauseMs = config.TtsImmersivePauseMs,
        };
    }

    /// <summary>
    /// Update the integrations' configuration.
    /// </summary>
    [HttpPut("config")]
    public async Task<IActionResult> UpdateIntegrationsConfig(
        [FromBody] UpdateIntegrationsConfigDto dto,
        CancellationToken cancellationToken = default)
    {
        var config = await _integrationsService.GetConfigAsync(cancellationToken);
        var invalidatedTags = new HashSet<string>();
        
        if (!string.IsNullOrWhiteSpace(dto.OpenRouterApiKey))
        {
            config.OpenRouterApiKey = dto.OpenRouterApiKey;
        }
        
        if (!string.IsNullOrWhiteSpace(dto.GoogleGenAiApiKey))
        {
            config.GoogleGenAiApiKey = dto.GoogleGenAiApiKey;
        }
        
        if (!string.IsNullOrWhiteSpace(dto.ElevenLabsApiKey))
        {
            config.ElevenLabsApiKey = dto.ElevenLabsApiKey;
            invalidatedTags.Add(Enums.TtsProvider.ElevenLabs.ToString());
        }
        
        if (!string.IsNullOrWhiteSpace(dto.UnrealSpeechApiKey))
        {
            config.UnrealSpeechApiKey = dto.UnrealSpeechApiKey;
            invalidatedTags.Add(Enums.TtsProvider.UnrealSpeech.ToString());
        }

        if (!string.IsNullOrWhiteSpace(dto.DeApiApiKey))
        {
            config.DeApiApiKey = dto.DeApiApiKey;
            invalidatedTags.Add(Enums.TtsProvider.DeApi.ToString());
        }

        if (!string.IsNullOrWhiteSpace(dto.NanoGptApiKey))
        {
            config.NanoGptApiKey = dto.NanoGptApiKey;
            invalidatedTags.Add(Enums.TtsProvider.NanoGpt.ToString());
        }

        if (dto.CustomTtsBaseUrl is not null)
        {
            config.CustomTtsBaseUrl = ResolveBaseUrl(
                dto.CustomTtsBaseUrl,
                IntegrationsConfig.DefaultCustomTtsBaseUrl);
            invalidatedTags.Add(Enums.TtsProvider.Custom.ToString());
        }

        if (dto.PocketTtsBaseUrl is not null)
        {
            config.PocketTtsBaseUrl = ResolveBaseUrl(
                dto.PocketTtsBaseUrl,
                IntegrationsConfig.DefaultPocketTtsBaseUrl);
            invalidatedTags.Add(Enums.TtsProvider.PocketTts.ToString());
        }

        if (dto.VibeVoiceBaseUrl is not null)
        {
            config.VibeVoiceBaseUrl = ResolveBaseUrl(
                dto.VibeVoiceBaseUrl,
                IntegrationsConfig.DefaultVibeVoiceBaseUrl);
            invalidatedTags.Add(Enums.TtsProvider.VibeVoice.ToString());
        }

        if (dto.ChatterboxBaseUrl is not null)
        {
            config.ChatterboxBaseUrl = ResolveBaseUrl(
                dto.ChatterboxBaseUrl,
                IntegrationsConfig.DefaultChatterboxBaseUrl);
            invalidatedTags.Add(Enums.TtsProvider.Chatterbox.ToString());
        }

        if (dto.Qwen3BaseUrl is not null)
        {
            config.Qwen3BaseUrl = ResolveBaseUrl(
                dto.Qwen3BaseUrl,
                IntegrationsConfig.DefaultQwen3BaseUrl);
            invalidatedTags.Add(Enums.TtsProvider.Qwen3.ToString());
        }

        if (dto.OmniVoiceBaseUrl is not null)
        {
            config.OmniVoiceBaseUrl = ResolveBaseUrl(
                dto.OmniVoiceBaseUrl,
                IntegrationsConfig.DefaultOmniVoiceBaseUrl);
            invalidatedTags.Add(Enums.TtsProvider.OmniVoice.ToString());
        }
        
        if (dto.TextGenerationProvider.HasValue)
        {
            config.TextGenerationProvider = dto.TextGenerationProvider.Value;
        }

        if (!string.IsNullOrWhiteSpace(dto.TextGenerationModelId))
        {
            config.TextGenerationModelId = dto.TextGenerationModelId;
        }

        if (dto.TtsProvider.HasValue)
        {
            config.TtsProvider = dto.TtsProvider.Value;
        }

        if (dto.ImageGenerationProvider.HasValue)
        {
            config.ImageGenerationProvider = dto.ImageGenerationProvider.Value;
        }

        if (dto.VideoGenerationProvider.HasValue)
        {
            config.VideoGenerationProvider = dto.VideoGenerationProvider.Value;
        }

        if (!string.IsNullOrWhiteSpace(dto.TtsModelId))
        {
            config.TtsModelId = dto.TtsModelId;
        }
        
        if (!string.IsNullOrWhiteSpace(dto.TtsVoiceId))
        {
            config.TtsVoiceId = dto.TtsVoiceId;
        }

        if (dto.TtsEnableTextEmphasis.HasValue)
        {
            config.TtsEnableTextEmphasis = dto.TtsEnableTextEmphasis.Value;
        }

        if (dto.TtsEnableImmersive.HasValue)
        {
            config.TtsEnableImmersive = dto.TtsEnableImmersive.Value;
        }

        if (dto.TtsImmersivePauseMs.HasValue)
        {
            config.TtsImmersivePauseMs = dto.TtsImmersivePauseMs.Value;
        }
        
        await _integrationsService.UpdateConfigAsync(config, cancellationToken);

        foreach (var tag in invalidatedTags)
        {
            await _hybridCache.RemoveByTagAsync(tag, cancellationToken);
        }

        _logger.LogInformation("Integrations config updated");
        return NoContent();
    }

    private static string ResolveBaseUrl(string configuredValue, string defaultValue) =>
        string.IsNullOrWhiteSpace(configuredValue)
            ? defaultValue
            : configuredValue.Trim();
}
