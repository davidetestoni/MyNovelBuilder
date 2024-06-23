namespace MyNovelBuilder.WebApi.Dtos.CompendiumRecord;

/// <summary>
/// DTO for a compendium record image.
/// </summary>
public class CompendiumRecordImageDto
{
    /// <summary>
    /// The image's ID.
    /// </summary>
    public Guid Id { get; set; }
    
    /// <summary>
    /// The image's URL.
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// Whether the image is the current image.
    /// </summary>
    public bool IsCurrent { get; set; }
}
