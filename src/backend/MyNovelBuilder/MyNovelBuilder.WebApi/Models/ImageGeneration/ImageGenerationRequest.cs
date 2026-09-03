namespace MyNovelBuilder.WebApi.Models.ImageGeneration;

/// <summary>
/// A request to generate an image.
/// </summary>
public class ImageGenerationRequest
{
    /// <summary>
    /// The model ID to use for generating the image.
    /// </summary>
    public required string ModelId { get; set; }
    
    /// <summary>
    /// The prompt to generate the image from.
    /// </summary>
    public required string Prompt { get; set; }
    
    /// <summary>
    /// The width of the image to generate, in pixels.
    /// </summary>
    public int Width { get; set; }
    
    /// <summary>
    /// The height of the image to generate, in pixels.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Whether to use a proxy for the request.
    /// </summary>
    public bool UseProxy { get; set; } = false;
}
