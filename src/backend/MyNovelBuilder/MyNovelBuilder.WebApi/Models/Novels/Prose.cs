namespace MyNovelBuilder.WebApi.Models.Novels;

/// <summary>
/// The prose of a novel.
/// </summary>
public class Prose
{
    /// <summary>
    /// The chapters of the novel.
    /// </summary>
    public IEnumerable<Chapter> Chapters { get; set; } = Array.Empty<Chapter>();
}
