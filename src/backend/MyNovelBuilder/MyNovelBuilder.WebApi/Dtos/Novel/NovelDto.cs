namespace MyNovelBuilder.WebApi.Dtos.Novel;

/// <summary>
/// Data transfer object for a novel.
/// </summary>
public class NovelDto
{
    /// <summary>
    /// The novel's title.
    /// </summary>
    public required string Title { get; set; }
}
