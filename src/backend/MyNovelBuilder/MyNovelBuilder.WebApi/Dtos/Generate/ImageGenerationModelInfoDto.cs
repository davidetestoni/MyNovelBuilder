namespace MyNovelBuilder.WebApi.Dtos.Generate;

/// <summary>
/// DTO representing an image generation model.
/// </summary>
public class ImageGenerationModelInfoDto
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
    /// Whether this model is an image editor.
    /// </summary>
    public required bool IsImageEditor { get; set; }
}