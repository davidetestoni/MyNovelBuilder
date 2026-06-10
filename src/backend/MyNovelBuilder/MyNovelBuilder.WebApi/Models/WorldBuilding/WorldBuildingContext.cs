namespace MyNovelBuilder.WebApi.Models.WorldBuilding;

/// <summary>
/// Context selected for a world-building session.
/// </summary>
public class WorldBuildingContext
{
    /// <summary>
    /// Optional novel used as additional context.
    /// </summary>
    public Guid? NovelId { get; set; }

    /// <summary>
    /// Optional chapter index used as prose context when a novel is selected.
    /// </summary>
    public int? ChapterIndex { get; set; }

    /// <summary>
    /// Compendia included in the session context.
    /// </summary>
    public IList<Guid> CompendiumIds { get; set; } = [];

    /// <summary>
    /// Specific compendium records included in the session context.
    /// </summary>
    public IList<Guid> CompendiumRecordIds { get; set; } = [];

    /// <summary>
    /// Freeform world premise for sessions that start before records exist.
    /// </summary>
    public string? FreeformPremise { get; set; }
}
