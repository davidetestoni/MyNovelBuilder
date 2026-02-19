namespace MyNovelBuilder.WebApi.Models.Novels;

/// <summary>
/// A chapter.
/// </summary>
public class Chapter
{
    /// <summary>
    /// The title of the chapter.
    /// </summary>
    public required string Title { get; set; }
    
    /// <summary>
    /// The sections of the chapter.
    /// </summary>
    public IList<Section> Sections { get; set; } = Array.Empty<Section>();
    
    /// <summary>
    /// The story events associated with the chapter.
    /// </summary>
    public StoryEvent[] StoryEvents { get; set; } = [];
}
