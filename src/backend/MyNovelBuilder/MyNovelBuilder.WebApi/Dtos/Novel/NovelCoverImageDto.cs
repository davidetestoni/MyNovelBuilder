namespace MyNovelBuilder.WebApi.Dtos.Novel;

/// <summary>
/// Data transfer object for a novel's cover image.
/// </summary>
public class NovelCoverImageDto
{
    /// <summary>
    /// The novel's ID.
    /// </summary>
    public Guid Id { get; set; }
    
    /// <summary>
    /// The novel's cover image location.
    /// Null if the novel does not have a cover image.
    /// </summary>
    public string? CoverImageLocation { get; set; }
}
