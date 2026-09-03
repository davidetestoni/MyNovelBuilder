using Microsoft.EntityFrameworkCore;
using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Models.Prompts;

namespace MyNovelBuilder.WebApi.Seeding;

internal sealed class InitialPromptSeeder
{
    internal const string MarkerKey = "initial-prompts";

    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<InitialPromptSeeder> _logger;
    private readonly Func<CancellationToken, Task<PromptSeedFixture>> _loadFixtureAsync;

    public InitialPromptSeeder(
        AppDbContext dbContext,
        TimeProvider timeProvider,
        ILogger<InitialPromptSeeder> logger)
        : this(
            dbContext,
            timeProvider,
            logger,
            PromptSeedFixtureLoader.LoadBundledAsync)
    {
    }

    internal InitialPromptSeeder(
        AppDbContext dbContext,
        TimeProvider timeProvider,
        ILogger<InitialPromptSeeder> logger,
        Func<CancellationToken, Task<PromptSeedFixture>> loadFixtureAsync)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _logger = logger;
        _loadFixtureAsync = loadFixtureAsync;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await IsCompleteAsync(cancellationToken))
        {
            return;
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            cancellationToken);

        if (await IsCompleteAsync(cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        if (await _dbContext.Prompts.AnyAsync(cancellationToken))
        {
            _logger.LogInformation(
                "Existing prompts found; skipping the one-time default prompt import.");
        }
        else
        {
            var fixture = await _loadFixtureAsync(cancellationToken);
            if (fixture.Prompts.Count == 0)
            {
                throw new InvalidDataException(
                    "The initial prompt fixture does not contain any prompts.");
            }

            _dbContext.Prompts.AddRange(fixture.Prompts.Select(CreatePrompt));
            _logger.LogInformation(
                "Importing {PromptCount} initial prompts.",
                fixture.Prompts.Count);
        }

        _dbContext.InitializationMarkers.Add(new InitializationMarker
        {
            Key = MarkerKey,
            CompletedAtUtc = _timeProvider.GetUtcNow().UtcDateTime
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private Task<bool> IsCompleteAsync(CancellationToken cancellationToken)
    {
        return _dbContext.InitializationMarkers.AnyAsync(
            marker => marker.Key == MarkerKey,
            cancellationToken);
    }

    private static Prompt CreatePrompt(PromptSeedDefinition definition)
    {
        return new Prompt
        {
            Name = definition.Name,
            Type = definition.Type,
            Messages = definition.Messages.Select(message => new PromptMessage
            {
                Role = message.Role,
                Message = message.Message
            }).ToList()
        };
    }
}
