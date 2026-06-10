using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Models.WorldBuilding;

namespace MyNovelBuilder.WebApi.Dtos.WorldBuilding;

/// <summary>
/// Structured output returned by the world-building agent.
/// </summary>
public class WorldBuildingAgentResponseDto
{
    /// <summary>
    /// Conversational assistant message.
    /// </summary>
    public string AssistantMessage { get; set; } = string.Empty;

    /// <summary>
    /// Proposed operations.
    /// </summary>
    public IEnumerable<WorldBuildingAgentProposalDto> Proposals { get; set; } = [];
}

/// <summary>
/// Proposal item returned by the model.
/// </summary>
public class WorldBuildingAgentProposalDto
{
    /// <summary>
    /// Proposed operation.
    /// </summary>
    public required WorldBuildingAgentOperationDto Operation { get; set; }

    /// <summary>
    /// Rationale for the operation.
    /// </summary>
    public string? Rationale { get; set; }
}

/// <summary>
/// Operation item returned by the model. Target IDs are parsed after deserialization
/// because models sometimes return placeholders instead of valid GUIDs.
/// </summary>
public class WorldBuildingAgentOperationDto
{
    /// <summary>
    /// The operation kind.
    /// </summary>
    public required WorldBuildingOperationKind Kind { get; set; }

    /// <summary>
    /// Target compendium ID as returned by the model.
    /// </summary>
    public string? TargetCompendiumId { get; set; }

    /// <summary>
    /// Target record ID as returned by the model.
    /// </summary>
    public string? TargetRecordId { get; set; }

    /// <summary>
    /// Proposed name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

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
    public CompendiumRecordType Type { get; set; } = CompendiumRecordType.Concept;

    /// <summary>
    /// Proposed record context.
    /// </summary>
    public string Context { get; set; } = string.Empty;

    /// <summary>
    /// Whether the record should always be included in prompt context.
    /// </summary>
    public bool AlwaysIncluded { get; set; }
}
