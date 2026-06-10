using System.Text.Json;
using System.Text.Json.Serialization;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Dtos.WorldBuilding;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Models.WorldBuilding;

namespace MyNovelBuilder.WebApi.Tests.Unit.Dtos.WorldBuilding;

public class WorldBuildingAgentResponseDtoTests
{
    private readonly JsonSerializerOptions _jsonOptions;

    public WorldBuildingAgentResponseDtoTests()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        _jsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }

    [Fact]
    public void Deserialize_WithPolymorphicProposalShapes_MapsToConcreteDtos()
    {
        // Arrange
        var compendiumId = Guid.NewGuid();
        var recordId = Guid.NewGuid();
        var json = $$"""
                     {
                       "assistantMessage": "I found a few useful changes.",
                       "proposals": [
                         {
                           "kind": "createCompendium",
                           "name": "Realm Almanac",
                           "description": "Primary setting reference.",
                           "rationale": "The world needs a home compendium."
                         },
                         {
                           "kind": "updateCompendium",
                           "targetCompendiumId": "{{compendiumId}}",
                           "name": "Realm Almanac",
                           "description": "Updated setting reference.",
                           "rationale": null
                         },
                         {
                           "kind": "createCompendiumRecord",
                           "targetCompendiumId": "{{compendiumId}}",
                           "name": "Mara Venn",
                           "aliases": "The Ash Cartographer",
                           "type": "character",
                           "context": "A mapmaker who records forbidden routes.",
                           "alwaysIncluded": true,
                           "rationale": "She anchors the travel conflicts."
                         },
                         {
                           "kind": "updateCompendiumRecord",
                           "targetRecordId": "{{recordId}}",
                           "name": "Old Gate",
                           "aliases": "North Gate",
                           "type": "place",
                           "context": "A gate whose locks respond to spoken oaths.",
                           "alwaysIncluded": false,
                           "rationale": "The existing place needs stronger hooks."
                         }
                       ]
                     }
                     """;

        // Act
        var response = JsonSerializer.Deserialize<WorldBuildingAgentResponseDto>(json, _jsonOptions)!;
        var proposals = response.Proposals.ToList();

        // Assert
        Assert.Equal("I found a few useful changes.", response.AssistantMessage);
        Assert.Collection(
            proposals,
            proposal => Assert.IsType<CreateCompendiumWorldBuildingAgentProposalDto>(proposal),
            proposal => Assert.IsType<UpdateCompendiumWorldBuildingAgentProposalDto>(proposal),
            proposal => Assert.IsType<CreateRecordWorldBuildingAgentProposalDto>(proposal),
            proposal => Assert.IsType<UpdateRecordWorldBuildingAgentProposalDto>(proposal));
    }

    [Fact]
    public void ToOperation_WithPolymorphicProposalShapes_MapsToInternalOperationModel()
    {
        // Arrange
        var compendiumId = Guid.NewGuid();
        var recordId = Guid.NewGuid();
        var json = $$"""
                     {
                       "assistantMessage": "Drafted changes.",
                       "proposals": [
                         {
                           "kind": "createCompendium",
                           "name": "Realm Almanac",
                           "description": "Primary setting reference.",
                           "rationale": null
                         },
                         {
                           "kind": "updateCompendium",
                           "targetCompendiumId": "{{compendiumId}}",
                           "name": "Realm Almanac",
                           "description": "Updated setting reference.",
                           "rationale": null
                         },
                         {
                           "kind": "createCompendiumRecord",
                           "targetCompendiumId": "{{compendiumId}}",
                           "name": "Mara Venn",
                           "aliases": "The Ash Cartographer",
                           "type": "character",
                           "context": "A mapmaker who records forbidden routes.",
                           "alwaysIncluded": true,
                           "rationale": null
                         },
                         {
                           "kind": "updateCompendiumRecord",
                           "targetRecordId": "{{recordId}}",
                           "name": "Old Gate",
                           "aliases": "North Gate",
                           "type": "place",
                           "context": "A gate whose locks respond to spoken oaths.",
                           "alwaysIncluded": false,
                           "rationale": null
                         }
                       ]
                     }
                     """;

        // Act
        var response = JsonSerializer.Deserialize<WorldBuildingAgentResponseDto>(json, _jsonOptions)!;
        var operations = response.Proposals.Select(proposal => proposal.ToOperation()).ToList();

        // Assert
        Assert.Collection(
            operations,
            operation =>
            {
                Assert.Equal(WorldBuildingOperationKind.CreateCompendium, operation.Kind);
                Assert.Equal("Realm Almanac", operation.Name);
                Assert.Equal("Primary setting reference.", operation.Description);
            },
            operation =>
            {
                Assert.Equal(WorldBuildingOperationKind.UpdateCompendium, operation.Kind);
                Assert.Equal(compendiumId, operation.TargetCompendiumId);
                Assert.Equal("Updated setting reference.", operation.Description);
            },
            operation =>
            {
                Assert.Equal(WorldBuildingOperationKind.CreateCompendiumRecord, operation.Kind);
                Assert.Equal(compendiumId, operation.TargetCompendiumId);
                Assert.Equal(CompendiumRecordType.Character, operation.Type);
                Assert.True(operation.AlwaysIncluded);
            },
            operation =>
            {
                Assert.Equal(WorldBuildingOperationKind.UpdateCompendiumRecord, operation.Kind);
                Assert.Equal(recordId, operation.TargetRecordId);
                Assert.Equal(CompendiumRecordType.Place, operation.Type);
                Assert.False(operation.AlwaysIncluded);
            });
    }

    [Fact]
    public void StructuredOutputSchema_UsesNestedAnyOfForProposalItems()
    {
        // Arrange
        var contextInfo = new WorldBuildingAgentContextInfoDto();

        // Act
        var structuredOutput = contextInfo.GetStructuredOutputOptions()!;
        using var schema = JsonDocument.Parse(structuredOutput.JsonSchema);
        var root = schema.RootElement;
        var proposalItems = root
            .GetProperty("properties")
            .GetProperty("proposals")
            .GetProperty("items");

        // Assert
        Assert.False(root.TryGetProperty("anyOf", out _));
        Assert.Equal("object", root.GetProperty("type").GetString());
        Assert.Equal(4, proposalItems.GetProperty("anyOf").GetArrayLength());
        Assert.True(structuredOutput.Strict);
        Assert.Equal("world_building_agent_response", structuredOutput.SchemaName);
    }
}
