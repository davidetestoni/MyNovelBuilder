namespace MyNovelBuilder.WebApi.Models.ImageGeneration;

/// <summary>
/// Information about an image generation model.
/// </summary>
public class ImageGenerationModelInfo
{
    /// <summary>
    /// The ID of the image generation model.
    /// </summary>
    public required string ModelId { get; set; }

    /// <summary>
    /// The name of the image generation model.
    /// </summary>
    public required string Name { get; set; }
}