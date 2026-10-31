using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Data.Repositories;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Models.Prompts;

namespace MyNovelBuilder.WebApi.Tests.Integration;

public sealed class PromptRepositoryTests
{
    [Fact]
    public async Task GetWithMessagesByIdAsync_LoadsPersistedMessagesAndReturnsNullForMissingPrompt()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var prompt = new Prompt
        {
            Name = "Stored prompt",
            Type = PromptType.GenerateText,
            Messages =
            [
                new PromptMessage { Role = PromptMessageRole.System, Message = "System instruction" },
                new PromptMessage { Role = PromptMessageRole.User, Message = "User request" }
            ]
        };
        context.Prompts.Add(prompt);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var repository = new PromptRepository(context);

        var loaded = await repository.GetWithMessagesByIdAsync(prompt.Id);

        Assert.NotNull(loaded);
        Assert.Equal(prompt.Id, loaded.Id);
        Assert.Collection(loaded.Messages,
            message =>
            {
                Assert.Equal(PromptMessageRole.System, message.Role);
                Assert.Equal("System instruction", message.Message);
            },
            message =>
            {
                Assert.Equal(PromptMessageRole.User, message.Role);
                Assert.Equal("User request", message.Message);
            });
        Assert.Null(await repository.GetWithMessagesByIdAsync(Guid.NewGuid()));
    }
}
