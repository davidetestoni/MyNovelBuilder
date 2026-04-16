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
/// Service for creating novel-scoped prompts.
/// </summary>
public class NovelPromptCreatorService : INovelPromptCreatorService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<NovelPromptCreatorService> _logger;

    /// <summary></summary>
    public NovelPromptCreatorService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<NovelPromptCreatorService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ProcessedPrompt> CreatePromptAsync(
        GenerateTextRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var novelService = scope.ServiceProvider.GetRequiredService<INovelService>();

        var prompt = await unitOfWork.Prompts.GetByIdAsync(request.PromptId, cancellationToken);
        if (prompt is null)
        {
            throw new ApiException(ErrorCodes.PromptNotFound,
                $"Prompt with ID {request.PromptId} not found.");
        }

        if (request.ContextInfo is not NovelTextGenerationContextInfoDto novelContextInfo)
        {
            throw new ApiException(ErrorCodes.InvalidPromptContext,
                "The prompt context is invalid.");
        }

        var requiredContextType = prompt.Type switch
        {
            PromptType.GenerateText => typeof(GenerateTextContextInfoDto),
            PromptType.SummarizeText => typeof(SummarizeTextContextInfoDto),
            PromptType.ReplaceText => typeof(ReplaceTextContextInfoDto),
            PromptType.CreateCompendiumRecord => typeof(CreateCompendiumRecordContextInfoDto),
            PromptType.EditCompendiumRecord => typeof(EditCompendiumRecordContextInfoDto),
            PromptType.SendChatMessage => typeof(SendChatMessageContextInfoDto),
            PromptType.PrepareImmersiveTts => typeof(PrepareImmersiveTtsContextInfoDto),
            PromptType.CreateStoryEvents => typeof(CreateStoryEventsContextInfoDto),
            PromptType.TranslateNovel => typeof(TranslateNovelContextInfoDto),
            _ => throw new ApiException(ErrorCodes.InvalidPromptContext,
                "This prompt type is not valid for novel prompt generation.")
        };

        if (request.ContextInfo.GetType() != requiredContextType)
        {
            throw new ApiException(ErrorCodes.InvalidPromptContext,
                "The prompt context is invalid.");
        }

        var novel = await unitOfWork.Novels.GetWithReferencesByIdAsync(novelContextInfo.NovelId, cancellationToken);
        if (novel is null)
        {
            throw new ApiException(ErrorCodes.NovelNotFound,
                $"Novel with ID {novelContextInfo.NovelId} not found.");
        }

        var prose = await novelService.GetProseAsync(novelContextInfo.NovelId, cancellationToken);
        var recordsTasks = novel.Compendia.Select(compendium =>
            unitOfWork.CompendiumRecords.GetByCompendiumIdAsync(compendium.Id, cancellationToken));
        var recordsLists = await Task.WhenAll(recordsTasks);
        var records = recordsLists.SelectMany(r => r).ToList();

        var processedPrompt = request.ContextInfo switch
        {
            GenerateTextContextInfoDto g => ProcessPrompt(g, prompt, novel, prose, records),
            SummarizeTextContextInfoDto s => ProcessPrompt(s, prompt, novel, prose, records),
            ReplaceTextContextInfoDto r => ProcessPrompt(r, prompt, novel, prose, records),
            CreateCompendiumRecordContextInfoDto c => ProcessPrompt(c, prompt, novel, prose, records),
            EditCompendiumRecordContextInfoDto e => ProcessPrompt(e, prompt, novel, prose, records),
            SendChatMessageContextInfoDto m => ProcessPrompt(m, prompt, novel, prose, records),
            PrepareImmersiveTtsContextInfoDto i => ProcessPrompt(i, prompt, novel, prose, records),
            CreateStoryEventsContextInfoDto s => ProcessPrompt(s, prompt, novel, prose, records),
            TranslateNovelContextInfoDto t => ProcessPrompt(t, prompt, novel, prose, records),
            _ => throw new ApiException(ErrorCodes.InvalidPromptContext,
                "The prompt context is invalid.")
        };

        _logger.LogInformation("Sending novel prompt with messages: {@Messages}", processedPrompt.Messages);
        return processedPrompt;
    }

    private static ProcessedPrompt ProcessPrompt<T>(T clientContext,
        Prompt prompt, Novel novel, Prose prose, List<CompendiumRecord> records)
        where T : TextGenerationContextInfoDto
    {
        var includedCompendiumRecordIds = new HashSet<Guid>();
        var messages = prompt.Messages.Select(message => new PromptMessage
        {
            Role = message.Role,
            Message = clientContext switch
            {
                GenerateTextContextInfoDto g => new GenerateTextPromptBuilder(message.Message)
                    .ReplacePlaceholders(new NovelPromptBuilderContext<GenerateTextContextInfoDto>
                    {
                        Client = g,
                        Novel = novel,
                        Prose = prose,
                        CompendiumRecords = records,
                        IncludedCompendiumRecordIds = includedCompendiumRecordIds
                    }).ToString(),
                SummarizeTextContextInfoDto s => new SummarizeTextPromptBuilder(message.Message)
                    .ReplacePlaceholders(new NovelPromptBuilderContext<SummarizeTextContextInfoDto>
                    {
                        Client = s,
                        Novel = novel,
                        Prose = prose,
                        CompendiumRecords = records,
                        IncludedCompendiumRecordIds = includedCompendiumRecordIds
                    }).ToString(),
                ReplaceTextContextInfoDto r => new ReplaceTextPromptBuilder(message.Message)
                    .ReplacePlaceholders(new NovelPromptBuilderContext<ReplaceTextContextInfoDto>
                    {
                        Client = r,
                        Novel = novel,
                        Prose = prose,
                        CompendiumRecords = records,
                        IncludedCompendiumRecordIds = includedCompendiumRecordIds
                    }).ToString(),
                CreateCompendiumRecordContextInfoDto c => new CreateCompendiumRecordPromptBuilder(message.Message)
                    .ReplacePlaceholders(new NovelPromptBuilderContext<CreateCompendiumRecordContextInfoDto>
                    {
                        Client = c,
                        Novel = novel,
                        Prose = prose,
                        CompendiumRecords = records,
                        IncludedCompendiumRecordIds = includedCompendiumRecordIds
                    }).ToString(),
                SendChatMessageContextInfoDto s => new SendChatMessagePromptBuilder(message.Message)
                    .ReplacePlaceholders(new NovelPromptBuilderContext<SendChatMessageContextInfoDto>
                    {
                        Client = s,
                        Novel = novel,
                        Prose = prose,
                        CompendiumRecords = records,
                        IncludedCompendiumRecordIds = includedCompendiumRecordIds
                    }).ToString(),
                PrepareImmersiveTtsContextInfoDto i => new PrepareImmersiveTtsPromptBuilder(message.Message)
                    .ReplacePlaceholders(new NovelPromptBuilderContext<PrepareImmersiveTtsContextInfoDto>
                    {
                        Client = i,
                        Novel = novel,
                        Prose = prose,
                        CompendiumRecords = records,
                        IncludedCompendiumRecordIds = includedCompendiumRecordIds
                    }).ToString(),
                CreateStoryEventsContextInfoDto s => new CreateStoryEventsPromptBuilder(message.Message)
                    .ReplacePlaceholders(new NovelPromptBuilderContext<CreateStoryEventsContextInfoDto>
                    {
                        Client = s,
                        Novel = novel,
                        Prose = prose,
                        CompendiumRecords = records,
                        IncludedCompendiumRecordIds = includedCompendiumRecordIds
                    }).ToString(),
                TranslateNovelContextInfoDto t => new TranslateNovelPromptBuilder(message.Message)
                    .ReplacePlaceholders(new NovelPromptBuilderContext<TranslateNovelContextInfoDto>
                    {
                        Client = t,
                        Novel = novel,
                        Prose = prose,
                        CompendiumRecords = records,
                        IncludedCompendiumRecordIds = includedCompendiumRecordIds
                    }).ToString(),
                _ => throw new ApiException(ErrorCodes.InvalidPromptContext,
                    "The prompt context is invalid.")
            }
        }).ToList();

        return new ProcessedPrompt
        {
            Messages = messages,
            IncludedCompendiumRecordIds = includedCompendiumRecordIds.OrderBy(id => id).ToList()
        };
    }
}
