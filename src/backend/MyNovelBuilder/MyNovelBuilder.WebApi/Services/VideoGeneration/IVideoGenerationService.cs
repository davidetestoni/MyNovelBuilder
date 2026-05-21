using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Models.VideoGeneration;

namespace MyNovelBuilder.WebApi.Services.VideoGeneration;

/// <summary>
/// Service for generating videos.
/// </summary>
public interface IVideoGenerationService
{
    /// <summary>
    /// Generate a video from the given prompt.
    /// </summary>
    Task<byte[]> GenerateVideoAsync(VideoGenerationRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate a video from an existing image and prompt.
    /// </summary>
    Task<byte[]> GenerateVideoFromImageAsync(
        byte[] imageBytes,
        VideoGenerationRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a list of available video generation models.
    /// </summary>
    Task<IEnumerable<VideoGenerationModelInfo>> GetAvailableModelsAsync(CancellationToken cancellationToken = default);
}
