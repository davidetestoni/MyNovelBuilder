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
    public async Task<IntegrationsConfigDto> GetIntegrationsConfig()
    {
        var config = await _integrationsService.GetConfigAsync();
        return new IntegrationsConfigDto
        {
            HasOpenRouterApiKey = !string.IsNullOrWhiteSpace(config.OpenRouterApiKey),
            TtsProvider = config.TtsProvider
        };
    }

    /// <summary>
    /// Update the integrations' configuration.
    /// </summary>
    [HttpPut("config")]
    public async Task<IActionResult> UpdateIntegrationsConfig([FromBody] UpdateIntegrationsConfigDto dto)
    {
        var config = await _integrationsService.GetConfigAsync();
        
        if (!string.IsNullOrWhiteSpace(dto.OpenRouterApiKey))
        {
            config.OpenRouterApiKey = dto.OpenRouterApiKey;
        }

        if (dto.TtsProvider.HasValue)
        {
            config.TtsProvider = dto.TtsProvider.Value;
        }
        
        await _integrationsService.UpdateConfigAsync(config);
        _logger.LogInformation("Integrations config updated");
        return NoContent();
    }
}
