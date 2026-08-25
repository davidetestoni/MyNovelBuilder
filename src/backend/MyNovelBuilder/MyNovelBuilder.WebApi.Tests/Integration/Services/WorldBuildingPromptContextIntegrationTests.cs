using Microsoft.Extensions.DependencyInjection;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.WorldBuilding;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Models.Chats;
using MyNovelBuilder.WebApi.Models.Prompts;
using MyNovelBuilder.WebApi.Models.WorldBuilding;
using MyNovelBuilder.WebApi.Services;
using MyNovelBuilder.WebApi.Tests.Factories;
using Xunit.Abstractions;

namespace MyNovelBuilder.WebApi.Tests.Integration.Services;

public class WorldBuildingPromptContextIntegrationTests(
    TestWebApplicationFactory<Program> factory,
    ITestOutputHelper output)
    : ControllerIntegrationTests(factory, output), IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await ResetDbAsync();
    }

    [Fact]
    public async Task Preview_UsesAllRecordsInSelectedScope_AndStructuredHistory()
    {
        var firstRecord = new CompendiumRecord
        {
            Name = "Glass Harbor",
            Type = CompendiumRecordType.Place,
            Context = "A harbor built from blue glass."
        };
        var secondRecord = new CompendiumRecord
        {
            Name = "Tide Guild",
            Type = CompendiumRecordType.Concept,
            Context = "The guild controls every ship entering the harbor."
        };
        var selectedCompendium = new Compendium
        {
            Name = "Selected World",
            Records = [firstRecord, secondRecord]
        };
        firstRecord.Compendium = selectedCompendium;
        secondRecord.Compendium = selectedCompendium;

        var outsideRecord = new CompendiumRecord
        {
            Name = "Outside Record",
            Type = CompendiumRecordType.Other,
            Context = "This must not be included in the selected scope."
        };
        var outsideCompendium = new Compendium
        {
            Name = "Outside World",
            Records = [outsideRecord]
        };
        outsideRecord.Compendium = outsideCompendium;

        var prompt = new Prompt
        {
            Name = "World builder test",
            Type = PromptType.WorldBuildingAgent,
            Messages =
            [
                new PromptMessage
                {
                    Role = PromptMessageRole.User,
                    Message = "{{records}}\n--- HISTORY ---\n{{chatHistory}}\n--- REQUEST ---\n{{instructions}}"
                }
            ]
        };

        UnitOfWork.Compendia.AddRange([selectedCompendium, outsideCompendium]);
        UnitOfWork.Prompts.Add(prompt);
        await UnitOfWork.SaveChangesAsync();

        var assistantMessageId = Guid.NewGuid();
        var session = new WorldBuildingSession
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Context = new WorldBuildingContext
            {
                CompendiumIds = [selectedCompendium.Id],
                CompendiumRecordIds = []
            },
            Messages =
            [
                new ChatMessage
                {
                    Id = assistantMessageId,
                    SentAt = DateTime.UtcNow,
                    Role = ChatMessageRole.Assistant,
                    TextContent = "I proposed the Lantern Court."
                }
            ],
            Proposals =
            [
                new WorldBuildingProposal
                {
                    Id = Guid.NewGuid(),
                    MessageId = assistantMessageId,
                    Status = WorldBuildingProposalStatus.Accepted,
                    Operation = new WorldBuildingOperation
                    {
                        Kind = WorldBuildingOperationKind.CreateCompendiumRecord,
                        TargetCompendiumId = selectedCompendium.Id,
                        Name = "Lantern Court",
                        Type = CompendiumRecordType.Place,
                        Context = "A court illuminated by captive stars.",
                        AlwaysIncluded = false
                    },
                    Rationale = "It gives the harbor a political center."
                }
            ]
        };

        using var scope = Factory.Services.CreateScope();
        var sessionService = scope.ServiceProvider
            .GetRequiredService<IWorldBuildingSessionService>();
        await sessionService.CreateAsync(session);

        var preview = await sessionService.GetMessagePreviewAsync(
            session.Id,
            new SendWorldBuildingMessageDto
            {
                Model = "unused-for-preview",
                PromptId = prompt.Id,
                Message = "Continue building"
            });

        Assert.Equal(
            new[] { firstRecord.Id, secondRecord.Id }.OrderBy(id => id),
            preview.IncludedCompendiumRecordIds.OrderBy(id => id));
        var finalPrompt = Assert.Single(preview.FinalMessages).Message;
        Assert.Contains(firstRecord.Context, finalPrompt);
        Assert.Contains(secondRecord.Context, finalPrompt);
        Assert.DoesNotContain(outsideRecord.Context, finalPrompt);
        Assert.Contains("createCompendiumRecord", finalPrompt);
        Assert.Contains("Lantern Court", finalPrompt);
        Assert.Contains("A court illuminated by captive stars.", finalPrompt);
    }

    [Fact]
    public async Task Preview_WithNoCompendiumOrRecordSelection_IncludesEverything()
    {
        var firstRecord = new CompendiumRecord
        {
            Name = "First Record",
            Type = CompendiumRecordType.Place,
            Context = "First record context."
        };
        var firstCompendium = new Compendium
        {
            Name = "First Compendium",
            Records = [firstRecord]
        };
        firstRecord.Compendium = firstCompendium;

        var secondRecord = new CompendiumRecord
        {
            Name = "Second Record",
            Type = CompendiumRecordType.Character,
            Context = "Second record context."
        };
        var secondCompendium = new Compendium
        {
            Name = "Second Compendium",
            Records = [secondRecord]
        };
        secondRecord.Compendium = secondCompendium;

        var prompt = new Prompt
        {
            Name = "All world context test",
            Type = PromptType.WorldBuildingAgent,
            Messages =
            [
                new PromptMessage
                {
                    Role = PromptMessageRole.User,
                    Message = "{{records}}"
                }
            ]
        };

        UnitOfWork.Compendia.AddRange([firstCompendium, secondCompendium]);
        UnitOfWork.Prompts.Add(prompt);
        await UnitOfWork.SaveChangesAsync();

        var session = new WorldBuildingSession
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Context = new WorldBuildingContext
            {
                CompendiumIds = [],
                CompendiumRecordIds = []
            }
        };

        using var scope = Factory.Services.CreateScope();
        var sessionService = scope.ServiceProvider
            .GetRequiredService<IWorldBuildingSessionService>();
        await sessionService.CreateAsync(session);

        var preview = await sessionService.GetMessagePreviewAsync(
            session.Id,
            new SendWorldBuildingMessageDto
            {
                Model = "unused-for-preview",
                PromptId = prompt.Id,
                Message = "Use everything"
            });

        Assert.Equal(
            new[] { firstRecord.Id, secondRecord.Id }.OrderBy(id => id),
            preview.IncludedCompendiumRecordIds.OrderBy(id => id));
        var finalPrompt = Assert.Single(preview.FinalMessages).Message;
        Assert.Contains(firstRecord.Context, finalPrompt);
        Assert.Contains(secondRecord.Context, finalPrompt);
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}
