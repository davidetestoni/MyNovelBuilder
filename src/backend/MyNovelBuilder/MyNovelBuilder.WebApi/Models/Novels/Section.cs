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
    /// The text of the section, in HTML format.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// The images associated with the section.
    /// </summary>
    public string[] Images { get; set; } = [];
    
    /// <summary>
    /// The record overrides associated with the section.
    /// </summary>
    public RecordOverride[] RecordOverrides { get; set; } = [];
}
