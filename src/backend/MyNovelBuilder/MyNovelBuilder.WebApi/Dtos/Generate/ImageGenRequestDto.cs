namespace MyNovelBuilder.WebApi.Dtos.Generate;

/// <summary>
/// DTO for an image generation request.
/// </summary>
public class ImageGenRequestDto
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
}
