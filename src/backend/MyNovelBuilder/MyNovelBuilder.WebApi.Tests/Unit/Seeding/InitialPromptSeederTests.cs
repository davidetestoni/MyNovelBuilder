using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Models.Prompts;
using MyNovelBuilder.WebApi.Seeding;

namespace MyNovelBuilder.WebApi.Tests.Unit.Seeding;

public sealed class InitialPromptSeederTests : IAsyncDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly AppDbContext dbContext;

    public InitialPromptSeederTests()
    {
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        dbContext = new AppDbContext(options);
        dbContext.Database.EnsureCreated();
    }

    [Fact]
    public async Task SeedAsync_ImportsBundledPromptsAndWritesMarker()
    {
        var seeder = CreateSeeder(PromptSeedFixtureLoader.LoadBundledAsync);

        await seeder.SeedAsync();

        Assert.Equal(15, await dbContext.Prompts.CountAsync());
        var marker = await dbContext.InitializationMarkers.SingleAsync();
        Assert.Equal(InitialPromptSeeder.MarkerKey, marker.Key);
        Assert.Equal(
            new DateTime(2026, 8, 28, 12, 0, 0, DateTimeKind.Utc),
            marker.CompletedAtUtc);
    }

    [Fact]
    public async Task SeedAsync_DoesNotRestoreEditedOrDeletedPrompts()
    {
        var seeder = CreateSeeder(PromptSeedFixtureLoader.LoadBundledAsync);
        await seeder.SeedAsync();
        var editedPrompt = await dbContext.Prompts.OrderBy(prompt => prompt.Id).FirstAsync();
        editedPrompt.Messages =
        [
            new PromptMessage
            {
                Role = PromptMessageRole.User,
                Message = "My edited prompt"
            }
        ];
        var deletedPrompt = await dbContext.Prompts
            .OrderBy(prompt => prompt.Id)
            .Skip(1)
            .FirstAsync();
        dbContext.Prompts.Remove(deletedPrompt);
        await dbContext.SaveChangesAsync();

        await seeder.SeedAsync();

        Assert.Equal(14, await dbContext.Prompts.CountAsync());
        Assert.Equal(
            "My edited prompt",
            Assert.Single(editedPrompt.Messages).Message);
    }

    [Fact]
    public async Task SeedAsync_LeavesExistingUnmarkedDatabaseUntouched()
    {
        dbContext.Prompts.Add(new Prompt
        {
            Name = "Existing prompt",
            Type = PromptType.GenerateText,
            Messages =
            [
                new PromptMessage
                {
                    Role = PromptMessageRole.User,
                    Message = "Keep me"
                }
            ]
        });
        await dbContext.SaveChangesAsync();
        var seeder = CreateSeeder(PromptSeedFixtureLoader.LoadBundledAsync);

        await seeder.SeedAsync();

        var prompt = await dbContext.Prompts.SingleAsync();
        Assert.Equal("Existing prompt", prompt.Name);
        Assert.True(await dbContext.InitializationMarkers.AnyAsync(
            marker => marker.Key == InitialPromptSeeder.MarkerKey));
    }

    [Fact]
    public async Task SeedAsync_DoesNotWriteMarkerWhenFixtureLoadingFails()
    {
        var seeder = CreateSeeder(
            _ => throw new InvalidDataException("Broken fixture"));

        await Assert.ThrowsAsync<InvalidDataException>(() => seeder.SeedAsync());

        Assert.Empty(await dbContext.Prompts.ToListAsync());
        Assert.Empty(await dbContext.InitializationMarkers.ToListAsync());
    }

    [Fact]
    public async Task SeedAsync_DoesNotReadFixtureAfterInitialization()
    {
        var seeder = CreateSeeder(PromptSeedFixtureLoader.LoadBundledAsync);
        await seeder.SeedAsync();
        var seederWithBrokenFixture = CreateSeeder(
            _ => throw new InvalidDataException("Should not be loaded"));

        await seederWithBrokenFixture.SeedAsync();

        Assert.Equal(15, await dbContext.Prompts.CountAsync());
    }

    public async ValueTask DisposeAsync()
    {
        await dbContext.DisposeAsync();
        await connection.DisposeAsync();
    }

    private InitialPromptSeeder CreateSeeder(
        Func<CancellationToken, Task<PromptSeedFixture>> loadFixtureAsync)
    {
        return new InitialPromptSeeder(
            dbContext,
            new FixedTimeProvider(
                new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero)),
            NullLogger<InitialPromptSeeder>.Instance,
            loadFixtureAsync);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
