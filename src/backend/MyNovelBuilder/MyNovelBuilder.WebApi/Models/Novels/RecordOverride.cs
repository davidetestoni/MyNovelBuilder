namespace MyNovelBuilder.WebApi.Models.Novels;

/// <summary>
/// A record override.
/// </summary>
public class RecordOverride
{
    /// <summary>
    /// The ID of the compendium record this override refers to.
    /// </summary>
    public Guid CompendiumRecordId { get; set; }
    
    /// <summary>
    /// The keyword for the override.
    /// </summary>
    public string Keyword { get; set; } = string.Empty;
    
    /// <summary>
    /// The new description for the override.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}
