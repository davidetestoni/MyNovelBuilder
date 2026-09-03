using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Models.WorldBuilding;

/// <summary>
/// A typed operation that can be applied after user approval.
/// </summary>
public class WorldBuildingOperation
{
    /// <summary>
    /// The operation kind.
    /// </summary>
    public required WorldBuildingOperationKind Kind { get; set; }

    /// <summary>
    /// Target compendium ID for updates or record creation.
    /// </summary>
    public Guid? TargetCompendiumId { get; set; }

    /// <summary>
    /// Target record ID for record updates.
    /// </summary>
    public Guid? TargetRecordId { get; set; }

    /// <summary>
    /// Proposed name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Proposed compendium description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Proposed record aliases.
    /// </summary>
    public string Aliases { get; set; } = string.Empty;

    /// <summary>
    /// Proposed record type.
    /// </summary>
    public required CompendiumRecordType Type { get; set; } = CompendiumRecordType.Concept;

    /// <summary>
    /// Proposed record context.
    /// </summary>
    public string Context { get; set; } = string.Empty;

    /// <summary>
    /// Whether the record should always be included in prompt context.
    /// </summary>
    public required bool AlwaysIncluded { get; set; }
}
