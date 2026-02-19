using Microsoft.AspNetCore.Mvc;
using MyNovelBuilder.WebApi.Dtos.Integrations;
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

    /// <summary></summary>
    public IntegrationsController(
        IIntegrationsService integrationsService,
        ILogger<IntegrationsController> logger)
    {
        _integrationsService = integrationsService;
        _logger = logger;
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
            TextGenerationProvider = config.TextGenerationProvider,
            TtsProvider = config.TtsProvider,
            ImageGenerationProvider = config.ImageGenerationProvider,
            TtsVoiceId = config.TtsVoiceId,
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
        }
        
        if (!string.IsNullOrWhiteSpace(dto.UnrealSpeechApiKey))
        {
            config.UnrealSpeechApiKey = dto.UnrealSpeechApiKey;
        }

        if (!string.IsNullOrWhiteSpace(dto.DeApiApiKey))
        {
            config.DeApiApiKey = dto.DeApiApiKey;
        }
        
        if (dto.TextGenerationProvider.HasValue)
        {
            config.TextGenerationProvider = dto.TextGenerationProvider.Value;
        }

        if (dto.TtsProvider.HasValue)
        {
            config.TtsProvider = dto.TtsProvider.Value;
        }

        if (dto.ImageGenerationProvider.HasValue)
        {
            config.ImageGenerationProvider = dto.ImageGenerationProvider.Value;
        }
        
        if (!string.IsNullOrWhiteSpace(dto.TtsVoiceId))
        {
            config.TtsVoiceId = dto.TtsVoiceId;
        }
        
        await _integrationsService.UpdateConfigAsync(config, cancellationToken);
        _logger.LogInformation("Integrations config updated");
        return NoContent();
    }
}
