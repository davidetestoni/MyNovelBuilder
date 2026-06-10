using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Models.Novels;
using MyNovelBuilder.WebApi.Models.Prompts;
using MyNovelBuilder.WebApi.Prompts.Builders;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for creating world-building prompts.
/// </summary>
public class WorldBuildingPromptCreatorService : IWorldBuildingPromptCreatorService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<WorldBuildingPromptCreatorService> _logger;

    /// <summary></summary>
    public WorldBuildingPromptCreatorService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<WorldBuildingPromptCreatorService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ProcessedPrompt> CreatePromptAsync(
        Prompt prompt,
        WorldBuildingAgentContextInfoDto context,
        CancellationToken cancellationToken = default)
    {
        if (prompt.Type != PromptType.WorldBuildingAgent)
        {
            throw new ApiException(
                ErrorCodes.InvalidPromptContext,
                "This prompt type is not valid for world-building agent generation.");
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var novelService = scope.ServiceProvider.GetRequiredService<INovelService>();

        Novel? novel = null;
        Prose? prose = null;

        if (context.NovelId.HasValue)
        {
            novel = await unitOfWork.Novels.GetWithReferencesByIdAsync(
                context.NovelId.Value,
                cancellationToken);

            if (novel is null)
            {
                throw new ApiException(
                    ErrorCodes.NovelNotFound,
                    $"Novel with ID {context.NovelId.Value} not found.");
            }

            prose = await novelService.GetProseAsync(context.NovelId.Value, cancellationToken);
        }

        var compendia = new List<Compendium>();
        foreach (var compendiumId in context.CompendiumIds.Distinct())
        {
            var compendium = await unitOfWork.Compendia.GetWithRecordsByIdAsync(
                compendiumId,
                cancellationToken);

            if (compendium is null)
            {
                throw new ApiException(
                    ErrorCodes.CompendiumNotFound,
                    $"Compendium with ID {compendiumId} not found.");
            }

            compendia.Add(compendium);
        }

        var records = compendia
            .SelectMany(compendium => compendium.Records)
            .Where(record => record.AlwaysIncluded || context.CompendiumRecordIds.Contains(record.Id))
            .ToList();

        var explicitlySelectedRecords = await unitOfWork.CompendiumRecords.GetByIdsAsync(
            context.CompendiumRecordIds,
            cancellationToken);

        records = records
            .Concat(explicitlySelectedRecords)
            .GroupBy(record => record.Id)
            .Select(group => group.First())
            .OrderBy(record => record.Name)
            .ToList();

        var includedRecordIds = new HashSet<Guid>();
        var messages = prompt.Messages.Select(message => new PromptMessage
        {
            Role = message.Role,
            Message = new WorldBuildingAgentPromptBuilder(message.Message)
                .ReplacePlaceholders(new WorldBuildingAgentPromptBuilderContext
                {
                    Client = context,
                    Novel = novel,
                    Prose = prose,
                    Compendia = compendia,
                    CompendiumRecords = records,
                    IncludedCompendiumRecordIds = includedRecordIds
                })
                .ToString()
        }).ToList();

        _logger.LogInformation(
            "Sending world-building prompt with messages: {@Messages}",
            messages);

        return new ProcessedPrompt
        {
            Messages = messages,
            IncludedCompendiumRecordIds = includedRecordIds.OrderBy(id => id).ToList()
        };
    }
}
