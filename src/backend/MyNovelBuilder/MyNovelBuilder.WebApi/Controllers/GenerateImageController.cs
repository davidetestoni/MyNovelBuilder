using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Hybrid;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Services;
using MyNovelBuilder.WebApi.Services.ImageGeneration;

namespace MyNovelBuilder.WebApi.Controllers;

/// <summary>
/// Controller for generating images.
/// </summary>
[Route("api/generate/image")]
[ApiController]
public class GenerateImageController : ControllerBase
{
    private readonly ILogger<GenerateImageController> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IIntegrationsService _integrationsService;
    private readonly HybridCache _hybridCache;

    /// <summary></summary>
    public GenerateImageController(
        ILogger<GenerateImageController> logger,
        IServiceProvider serviceProvider,
        IIntegrationsService integrationsService,
        HybridCache hybridCache)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _integrationsService = integrationsService;
        _hybridCache = hybridCache;
    }

    private async ValueTask<IImageGenerationService> GetImageGenerationServiceAsync()
    {
        var config = await _integrationsService.GetConfigAsync();
        var imageGenerationService = _serviceProvider.GetKeyedService<IImageGenerationService>(config.ImageGenerationProvider);

        if (imageGenerationService is null)
        {
            _logger.LogError(
                "Unsupported image generation provider: {Provider}", config.ImageGenerationProvider);

            throw new InvalidOperationException(
                $"Unsupported image generation provider: {config.ImageGenerationProvider}");
        }

        return imageGenerationService;
    }
    
    /// <summary>
    /// Generate an image.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult> GenerateImageAsync(ImageGenRequestDto dto)
    {
        var imageGen = await GetImageGenerationServiceAsync();
        var image = await imageGen.GenerateImageAsync(dto);
        
        return File(image, "image/png", "image.png");
    }

    /// <summary>
    /// Get available image generation models.
    /// </summary>
    /// <returns></returns>
    [HttpGet("models")]
    public async Task<IEnumerable<ImageGenerationModelInfoDto>> GetAvailableModelsAsync()
    {
        var config = await _integrationsService.GetConfigAsync();
        return await _hybridCache.GetOrCreateAsync(
            $"imagegen-{config.ImageGenerationProvider}-models",
            async _ =>
            {
                var imageGenerationService = await GetImageGenerationServiceAsync();
                var models = await imageGenerationService.GetAvailableModelsAsync();

                return models.Select(m => new ImageGenerationModelInfoDto
                {
                    ModelId = m.ModelId,
                    Name = m.Name,
                });
            },
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromHours(6),
                LocalCacheExpiration = TimeSpan.FromHours(6)
            },
            tags: ["imagegen", config.ImageGenerationProvider.ToString(), "models"]
        );
    }
}
