namespace MyNovelBuilder.WebApi.Models.VideoGeneration;

/// <summary>
/// Information about a video generation model.
/// </summary>
public class VideoGenerationModelInfo
{
    /// <summary>
    /// The ID of the video generation model.
    /// </summary>
    public required string ModelId { get; set; }

    /// <summary>
    /// The name of the video generation model.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Whether this model supports text-to-video generation.
    /// </summary>
    public required bool SupportsTextToVideo { get; set; }

    /// <summary>
    /// Whether this model supports image-to-video generation.
    /// </summary>
    public required bool SupportsImageToVideo { get; set; }
}
