using MyNovelBuilder.WebApi.Dtos.Generate;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for generating images.
/// </summary>
public interface IImageGenerationService
{
    /// <summary>
    /// Generate an image from the given prompt.
    /// </summary>
    Task<byte[]> GenerateImageAsync(ImageGenRequestDto request);
}
