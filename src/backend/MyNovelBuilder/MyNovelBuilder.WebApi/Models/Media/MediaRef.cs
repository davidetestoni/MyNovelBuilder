namespace MyNovelBuilder.WebApi.Models.Media;

/// <summary>
/// Reference to an image.
/// </summary>
public class MediaRef
{
    /// <summary>
    /// The image's ID.
    /// </summary>
    public required Guid Id { get; set; }
    
    /// <summary>
    /// The image's location.
    /// </summary>
    public required string Location { get; set; }
    
    /// <summary>
    /// Whether the image is a video.
    /// </summary>
    public required bool IsVideo { get; set; }
}
