namespace MyNovelBuilder.WebApi.Models.Novels;

/// <summary>
/// A section.
/// </summary>
public class Section
{
    /// <summary>
    /// The summary of the section.
    /// </summary>
    public string Summary { get; set; } = string.Empty;
    
    /// <summary>
    /// The items of the section.
    /// </summary>
    public required IEnumerable<SectionItem> Items { get; set; } = Array.Empty<SectionItem>();
}
