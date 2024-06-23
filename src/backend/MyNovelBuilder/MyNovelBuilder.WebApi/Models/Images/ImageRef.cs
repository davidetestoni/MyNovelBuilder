namespace MyNovelBuilder.WebApi.Models.Images;

/// <summary>
/// Reference to an image.
/// </summary>
public class ImageRef
{
/// <summary>
    /// The image's ID.
    /// </summary>
    public required Guid Id { get; set; }
    
    /// <summary>
    /// The image's location.
    /// </summary>
    public required string Location { get; set; }
}
