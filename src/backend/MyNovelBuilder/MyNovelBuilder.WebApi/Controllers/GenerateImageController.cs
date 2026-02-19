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

    private async ValueTask<IImageGenerationService> GetImageGenerationServiceAsync(
        CancellationToken cancellationToken = default)
    {
        var config = await _integrationsService.GetConfigAsync(cancellationToken);
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
    public async Task<ActionResult> GenerateImageAsync(
        ImageGenerationRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        var imageGen = await GetImageGenerationServiceAsync(cancellationToken);
        var image = await imageGen.GenerateImageAsync(dto, cancellationToken);
        
        return File(image, "image/png", "image.png");
    }
    
    /// <summary>
    /// Edit an existing image.
    /// </summary>
    [HttpPost("edit")]
    public async Task<ActionResult> EditImageAsync(
        IFormFile image,
        [FromForm] ImageGenerationRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        if (image.Length == 0)
        {
            return BadRequest("Image file is required.");
        }

        await using var imageStream = image.OpenReadStream();
        using var ms = new MemoryStream();
        await imageStream.CopyToAsync(ms, cancellationToken);
        var imageBytes = ms.ToArray();
        var imageGen = await GetImageGenerationServiceAsync(cancellationToken);
        var editedImage = await imageGen.EditImageAsync(imageBytes, dto, cancellationToken);
        
        return File(editedImage, "image/png", "edited_image.png");
    }

    /// <summary>
    /// Get available image generation models.
    /// </summary>
    /// <returns></returns>
    [HttpGet("models")]
    public async Task<IEnumerable<ImageGenerationModelInfoDto>> GetAvailableModelsAsync(
        CancellationToken cancellationToken = default)
    {
        var config = await _integrationsService.GetConfigAsync(cancellationToken);
        return await _hybridCache.GetOrCreateAsync(
            $"imagegen-{config.ImageGenerationProvider}-models",
            async token =>
            {
                var imageGenerationService = await GetImageGenerationServiceAsync(token);
                var models = await imageGenerationService.GetAvailableModelsAsync(token);

                return models.Select(m => new ImageGenerationModelInfoDto
                {
                    ModelId = m.ModelId,
                    Name = m.Name,
                    IsImageEditor = m.IsImageEditor,
                });
            },
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromHours(6),
                LocalCacheExpiration = TimeSpan.FromHours(6)
            },
            tags: ["imagegen", config.ImageGenerationProvider.ToString(), "models"],
            cancellationToken: cancellationToken
        );
    }
}
