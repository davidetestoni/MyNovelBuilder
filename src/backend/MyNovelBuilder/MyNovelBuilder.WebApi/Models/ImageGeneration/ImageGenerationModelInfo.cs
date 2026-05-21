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
    
    /// <summary>
    /// Whether this model supports text-to-image generation.
    /// </summary>
    public required bool SupportsImageGeneration { get; set; }

    /// <summary>
    /// Whether this model supports image-to-image generation.
    /// </summary>
    public required bool SupportsImageEditing { get; set; }

}
