using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Models.ImageGeneration;

namespace MyNovelBuilder.WebApi.Services.ImageGeneration;

/// <summary>
/// Service for generating images.
/// </summary>
public interface IImageGenerationService
{
    /// <summary>
    /// Generate an image from the given prompt.
    /// </summary>
    Task<byte[]> GenerateImageAsync(ImageGenerationRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Edit an existing image based on the given prompt.
    /// </summary>
    Task<byte[]> EditImageAsync(
        byte[] imageBytes,
        ImageGenerationRequestDto request,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a list of available image generation models.
    /// </summary>
    Task<IEnumerable<ImageGenerationModelInfo>> GetAvailableModelsAsync(CancellationToken cancellationToken = default);
}
