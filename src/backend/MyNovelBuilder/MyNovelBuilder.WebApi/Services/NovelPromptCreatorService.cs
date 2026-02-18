using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Dtos.Prompt;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Models.Novels;
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
    public async Task<IEnumerable<PromptMessageDto>> CreatePromptAsync(
        GenerateTextRequestDto request)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var novelService = scope.ServiceProvider.GetRequiredService<INovelService>();

        var prompt = await unitOfWork.Prompts.GetByIdAsync(request.PromptId);
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
            _ => throw new ApiException(ErrorCodes.InvalidPromptContext,
                "This prompt type is not valid for novel prompt generation.")
        };

        if (request.ContextInfo.GetType() != requiredContextType)
        {
            throw new ApiException(ErrorCodes.InvalidPromptContext,
                "The prompt context is invalid.");
        }

        var novel = await unitOfWork.Novels.GetWithReferencesByIdAsync(novelContextInfo.NovelId);
        if (novel is null)
        {
            throw new ApiException(ErrorCodes.NovelNotFound,
                $"Novel with ID {novelContextInfo.NovelId} not found.");
        }

        var prose = await novelService.GetProseAsync(novelContextInfo.NovelId);
        var recordsTasks = novel.Compendia.Select(compendium =>
            unitOfWork.CompendiumRecords.GetByCompendiumIdAsync(compendium.Id));
        var recordsLists = await Task.WhenAll(recordsTasks);
        var records = recordsLists.SelectMany(r => r).ToList();

        var messages = request.ContextInfo switch
        {
            GenerateTextContextInfoDto g => GetPromptMessages(g, prompt, novel, prose, records),
            SummarizeTextContextInfoDto s => GetPromptMessages(s, prompt, novel, prose, records),
            ReplaceTextContextInfoDto r => GetPromptMessages(r, prompt, novel, prose, records),
            CreateCompendiumRecordContextInfoDto c => GetPromptMessages(c, prompt, novel, prose, records),
            EditCompendiumRecordContextInfoDto e => GetPromptMessages(e, prompt, novel, prose, records),
            SendChatMessageContextInfoDto m => GetPromptMessages(m, prompt, novel, prose, records),
            _ => throw new ApiException(ErrorCodes.InvalidPromptContext,
                "The prompt context is invalid.")
        };

        _logger.LogInformation("Sending novel prompt with messages: {@Messages}", messages);
        return messages;
    }

    private static IEnumerable<PromptMessageDto> GetPromptMessages<T>(T clientContext,
        Prompt prompt, Novel novel, Prose prose, List<CompendiumRecord> records)
        where T : TextGenerationContextInfoDto
    {
        return prompt.Messages.Select(message => new PromptMessageDto
        {
            Role = message.Role,
            Message = clientContext switch
            {
                GenerateTextContextInfoDto g => new GenerateTextPromptBuilder(message.Message)
                    .ReplacePlaceholders(new PromptBuilderContext<GenerateTextContextInfoDto>
                    {
                        Client = g,
                        Novel = novel,
                        Prose = prose,
                        CompendiumRecords = records
                    }).ToString(),
                SummarizeTextContextInfoDto s => new SummarizeTextPromptBuilder(message.Message)
                    .ReplacePlaceholders(new PromptBuilderContext<SummarizeTextContextInfoDto>
                    {
                        Client = s,
                        Novel = novel,
                        Prose = prose,
                        CompendiumRecords = records
                    }).ToString(),
                ReplaceTextContextInfoDto r => new ReplaceTextPromptBuilder(message.Message)
                    .ReplacePlaceholders(new PromptBuilderContext<ReplaceTextContextInfoDto>
                    {
                        Client = r,
                        Novel = novel,
                        Prose = prose,
                        CompendiumRecords = records
                    }).ToString(),
                CreateCompendiumRecordContextInfoDto c => new CreateCompendiumRecordPromptBuilder(message.Message)
                    .ReplacePlaceholders(new PromptBuilderContext<CreateCompendiumRecordContextInfoDto>
                    {
                        Client = c,
                        Novel = novel,
                        Prose = prose,
                        CompendiumRecords = records
                    }).ToString(),
                SendChatMessageContextInfoDto s => new SendChatMessagePromptBuilder(message.Message)
                    .ReplacePlaceholders(new PromptBuilderContext<SendChatMessageContextInfoDto>
                    {
                        Client = s,
                        Novel = novel,
                        Prose = prose,
                        CompendiumRecords = records
                    }).ToString(),
                _ => throw new ApiException(ErrorCodes.InvalidPromptContext,
                    "The prompt context is invalid.")
            }
        });
    }
}
