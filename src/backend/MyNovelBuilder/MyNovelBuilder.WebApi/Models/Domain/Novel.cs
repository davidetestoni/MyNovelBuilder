namespace MyNovelBuilder.WebApi.Models.Domain;

/// <summary>
/// A novel.
/// </summary>
public class Novel
{
    /// <summary>
    /// The novel's title.
    /// </summary>
    public required string Title { get; set; }
}
