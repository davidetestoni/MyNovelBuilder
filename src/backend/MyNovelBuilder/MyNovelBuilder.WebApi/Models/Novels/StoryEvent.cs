namespace MyNovelBuilder.WebApi.Models.Novels;

/// <summary>
/// A story event.
/// </summary>
public class StoryEvent
{
    /// <summary>
    /// The title of the story event.
    /// </summary>
    public string Title { get; set; } = "Story Event";
    
    /// <summary>
    /// The date of the story event. Arbitrary format.
    /// </summary>
    public string Date { get; set; } = string.Empty;
    
    /// <summary>
    /// The description of the story event.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}