using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Hybrid;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Services;
using MyNovelBuilder.WebApi.Services.VideoGeneration;

namespace MyNovelBuilder.WebApi.Controllers;

/// <summary>
/// Controller for generating videos.
/// </summary>
[Route("api/generate/video")]
[ApiController]
public class GenerateVideoController : ControllerBase
{
    private readonly ILogger<GenerateVideoController> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IIntegrationsService _integrationsService;
    private readonly HybridCache _hybridCache;

    /// <summary></summary>
    public GenerateVideoController(
        ILogger<GenerateVideoController> logger,
        IServiceProvider serviceProvider,
        IIntegrationsService integrationsService,
        HybridCache hybridCache)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _integrationsService = integrationsService;
        _hybridCache = hybridCache;
    }

    private async ValueTask<IVideoGenerationService> GetVideoGenerationServiceAsync(
        CancellationToken cancellationToken = default)
    {
        var config = await _integrationsService.GetConfigAsync(cancellationToken);
        var videoGenerationService =
            _serviceProvider.GetKeyedService<IVideoGenerationService>(config.VideoGenerationProvider);

        if (videoGenerationService is null)
        {
            _logger.LogError(
                "Unsupported video generation provider: {Provider}",
                config.VideoGenerationProvider);

            throw new InvalidOperationException(
                $"Unsupported video generation provider: {config.VideoGenerationProvider}");
        }

        return videoGenerationService;
    }

    /// <summary>
    /// Generate a video.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult> GenerateVideoAsync(
        VideoGenerationRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        var videoGen = await GetVideoGenerationServiceAsync(cancellationToken);
        var video = await videoGen.GenerateVideoAsync(dto, cancellationToken);

        return File(video, "video/mp4", "video.mp4");
    }

    /// <summary>
    /// Generate a video from an image.
    /// </summary>
    [HttpPost("from-image")]
    public async Task<ActionResult> GenerateVideoFromImageAsync(
        IFormFile image,
        [FromForm] VideoGenerationRequestDto dto,
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
        var videoGen = await GetVideoGenerationServiceAsync(cancellationToken);
        var video = await videoGen.GenerateVideoFromImageAsync(imageBytes, dto, cancellationToken);

        return File(video, "video/mp4", "video-from-image.mp4");
    }

    /// <summary>
    /// Get available video generation models.
    /// </summary>
    [HttpGet("models")]
    public async Task<IEnumerable<VideoGenerationModelInfoDto>> GetAvailableModelsAsync(
        CancellationToken cancellationToken = default)
    {
        var config = await _integrationsService.GetConfigAsync(cancellationToken);
        return await _hybridCache.GetOrCreateAsync(
            $"videogen-{config.VideoGenerationProvider}-models",
            async token =>
            {
                var videoGenerationService = await GetVideoGenerationServiceAsync(token);
                var models = await videoGenerationService.GetAvailableModelsAsync(token);

                return models.Select(model => new VideoGenerationModelInfoDto
                {
                    ModelId = model.ModelId,
                    Name = model.Name,
                    SupportsTextToVideo = model.SupportsTextToVideo,
                    SupportsImageToVideo = model.SupportsImageToVideo,
                });
            },
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromHours(6),
                LocalCacheExpiration = TimeSpan.FromHours(6)
            },
            tags: ["videogen", config.VideoGenerationProvider.ToString(), "models"],
            cancellationToken: cancellationToken);
    }
}
