using System.Text.Json;
using System.Text.Json.Serialization;
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
/// Base proposal item returned by the model.
/// </summary>
[JsonConverter(typeof(WorldBuildingAgentProposalDtoJsonConverter))]
public abstract class WorldBuildingAgentProposalDto
{
    /// <summary>
    /// The operation kind.
    /// </summary>
    public WorldBuildingOperationKind Kind { get; set; }

    /// <summary>
    /// Rationale for the operation.
    /// </summary>
    public string? Rationale { get; set; }

    /// <summary>
    /// Converts the model-facing proposal shape to the internal operation model.
    /// </summary>
    public abstract WorldBuildingOperation ToOperation();

    /// <summary>
    /// Parses a nullable model-returned UUID, treating placeholders and invalid IDs as missing.
    /// </summary>
    protected static Guid? ParseNullableGuid(string? value)
    {
        return string.IsNullOrWhiteSpace(value) || !Guid.TryParse(value, out var id)
            ? null
            : id;
    }
}

/// <summary>
/// Create-compendium proposal returned by the model.
/// </summary>
public class CreateCompendiumWorldBuildingAgentProposalDto : WorldBuildingAgentProposalDto
{
    /// <summary></summary>
    public CreateCompendiumWorldBuildingAgentProposalDto()
    {
        Kind = WorldBuildingOperationKind.CreateCompendium;
    }

    /// <summary>
    /// Proposed compendium name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Proposed compendium description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <inheritdoc />
    public override WorldBuildingOperation ToOperation() => new()
    {
        Kind = WorldBuildingOperationKind.CreateCompendium,
        Name = Name,
        Description = Description,
        Type = CompendiumRecordType.Concept,
        AlwaysIncluded = false
    };
}

/// <summary>
/// Update-compendium proposal returned by the model.
/// </summary>
public class UpdateCompendiumWorldBuildingAgentProposalDto : WorldBuildingAgentProposalDto
{
    /// <summary></summary>
    public UpdateCompendiumWorldBuildingAgentProposalDto()
    {
        Kind = WorldBuildingOperationKind.UpdateCompendium;
    }

    /// <summary>
    /// Existing compendium UUID as returned by the model.
    /// </summary>
    public string? TargetCompendiumId { get; set; }

    /// <summary>
    /// Proposed compendium name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Proposed compendium description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <inheritdoc />
    public override WorldBuildingOperation ToOperation() => new()
    {
        Kind = WorldBuildingOperationKind.UpdateCompendium,
        TargetCompendiumId = ParseNullableGuid(TargetCompendiumId),
        Name = Name,
        Description = Description,
        Type = CompendiumRecordType.Concept,
        AlwaysIncluded = false
    };
}

/// <summary>
/// Create-record proposal returned by the model.
/// </summary>
public class CreateRecordWorldBuildingAgentProposalDto : WorldBuildingAgentProposalDto
{
    /// <summary></summary>
    public CreateRecordWorldBuildingAgentProposalDto()
    {
        Kind = WorldBuildingOperationKind.CreateCompendiumRecord;
    }

    /// <summary>
    /// Existing compendium UUID as returned by the model.
    /// </summary>
    public string? TargetCompendiumId { get; set; }

    /// <summary>
    /// Proposed record name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

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

    /// <inheritdoc />
    public override WorldBuildingOperation ToOperation() => new()
    {
        Kind = WorldBuildingOperationKind.CreateCompendiumRecord,
        TargetCompendiumId = ParseNullableGuid(TargetCompendiumId),
        Name = Name,
        Aliases = Aliases,
        Type = Type,
        Context = Context,
        AlwaysIncluded = AlwaysIncluded
    };
}

/// <summary>
/// Update-record proposal returned by the model.
/// </summary>
public class UpdateRecordWorldBuildingAgentProposalDto : WorldBuildingAgentProposalDto
{
    /// <summary></summary>
    public UpdateRecordWorldBuildingAgentProposalDto()
    {
        Kind = WorldBuildingOperationKind.UpdateCompendiumRecord;
    }

    /// <summary>
    /// Existing record UUID as returned by the model.
    /// </summary>
    public string? TargetRecordId { get; set; }

    /// <summary>
    /// Proposed record name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

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

    /// <inheritdoc />
    public override WorldBuildingOperation ToOperation() => new()
    {
        Kind = WorldBuildingOperationKind.UpdateCompendiumRecord,
        TargetRecordId = ParseNullableGuid(TargetRecordId),
        Name = Name,
        Aliases = Aliases,
        Type = Type,
        Context = Context,
        AlwaysIncluded = AlwaysIncluded
    };
}

internal class WorldBuildingAgentProposalDtoJsonConverter : JsonConverter<WorldBuildingAgentProposalDto>
{
    public override WorldBuildingAgentProposalDto Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);

        if (!document.RootElement.TryGetProperty("kind", out var kindElement))
        {
            throw new JsonException("World-building proposal kind is required.");
        }

        var kind = JsonSerializer.Deserialize<WorldBuildingOperationKind>(kindElement.GetRawText(), options);
        var json = document.RootElement.GetRawText();

        return kind switch
        {
            WorldBuildingOperationKind.CreateCompendium =>
                JsonSerializer.Deserialize<CreateCompendiumWorldBuildingAgentProposalDto>(json, options)!,
            WorldBuildingOperationKind.UpdateCompendium =>
                JsonSerializer.Deserialize<UpdateCompendiumWorldBuildingAgentProposalDto>(json, options)!,
            WorldBuildingOperationKind.CreateCompendiumRecord =>
                JsonSerializer.Deserialize<CreateRecordWorldBuildingAgentProposalDto>(json, options)!,
            WorldBuildingOperationKind.UpdateCompendiumRecord =>
                JsonSerializer.Deserialize<UpdateRecordWorldBuildingAgentProposalDto>(json, options)!,
            _ => throw new JsonException($"Unsupported world-building proposal kind: {kind}.")
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        WorldBuildingAgentProposalDto value,
        JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}
