namespace MyNovelBuilder.WebApi.Models.WorldBuilding;

/// <summary>
/// Supported world-building proposal operation kinds.
/// </summary>
public enum WorldBuildingOperationKind
{
    /// <summary>
    /// Create a compendium.
    /// </summary>
    CreateCompendium = 0,

    /// <summary>
    /// Update an existing compendium.
    /// </summary>
    UpdateCompendium = 1,

    /// <summary>
    /// Create a compendium record.
    /// </summary>
    CreateCompendiumRecord = 2,

    /// <summary>
    /// Update an existing compendium record.
    /// </summary>
    UpdateCompendiumRecord = 3
}
