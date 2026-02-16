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
    Task<byte[]> GenerateImageAsync(ImageGenRequestDto request);
    
    /// <summary>
    /// Get a list of available image generation models.
    /// </summary>
    Task<IEnumerable<ImageGenerationModelInfo>> GetAvailableModelsAsync();
}
