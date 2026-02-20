namespace MyNovelBuilder.WebApi.Dtos.Generate;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>
/// DTO for an image generation request.
/// </summary>
public class ImageGenerationRequestDto
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
    [JsonRequired]
    [Range(1, 10_000)]
    public int Width { get; set; }
    
    /// <summary>
    /// The height of the image to generate, in pixels.
    /// </summary>
    [JsonRequired]
    [Range(1, 10_000)]
    public int Height { get; set; }
}
