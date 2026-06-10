namespace MyNovelBuilder.WebApi.Models.WorldBuilding;

/// <summary>
/// Metadata for listing world-building sessions.
/// </summary>
public class WorldBuildingSessionMetadata
{
    /// <summary>
    /// The session ID.
    /// </summary>
    public required Guid Id { get; set; }

    /// <summary>
    /// Optional novel used as context.
    /// </summary>
    public Guid? NovelId { get; set; }

    /// <summary>
    /// The time the session was created.
    /// </summary>
    public required DateTime CreatedAt { get; set; }

    /// <summary>
    /// The time the session was last updated.
    /// </summary>
    public required DateTime UpdatedAt { get; set; }

    /// <summary>
    /// The session name.
    /// </summary>
    public string? Name { get; set; }
}
