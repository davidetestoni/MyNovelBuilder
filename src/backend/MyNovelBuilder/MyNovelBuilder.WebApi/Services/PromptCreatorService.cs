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
/// Service for creating prompts.
/// </summary>
public class PromptCreatorService : IPromptCreatorService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<PromptCreatorService> _logger;

    /// <summary></summary>
    public PromptCreatorService(IServiceScopeFactory serviceScopeFactory,
        ILogger<PromptCreatorService> logger)
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
        
        // Make sure the prompt and payload types match
        var requiredContextType = prompt.Type switch
        {
            PromptType.GenerateText => typeof(GenerateTextContextInfoDto),
            PromptType.SummarizeText => typeof(SummarizeTextContextInfoDto),
            PromptType.ReplaceText => typeof(ReplaceTextContextInfoDto),
            PromptType.CreateCompendiumRecord => typeof(CreateCompendiumRecordContextInfoDto),
            PromptType.EditCompendiumRecord => typeof(EditCompendiumRecordContextInfoDto),
            _ => throw new NotImplementedException("Unknown prompt type.")
        };
        
        if (request.ContextInfo.GetType() != requiredContextType)
        {
            throw new ApiException(ErrorCodes.InvalidPromptContext,
                "The prompt context is invalid.");
        }
        
        var novel = await unitOfWork.Novels.GetWithReferencesByIdAsync(request.NovelId);
        
        if (novel is null)
        {
            throw new ApiException(ErrorCodes.NovelNotFound,
                $"Novel with ID {request.NovelId} not found.");
        }

        var prose = await novelService.GetProseAsync(request.NovelId);
        
        var recordsTasks = novel.Compendia.Select(compendium =>
            unitOfWork.CompendiumRecords.GetByCompendiumIdAsync(compendium.Id));
        
        var recordsLists = await Task.WhenAll(recordsTasks);
        var records = recordsLists.SelectMany(r => r).ToList(); // Flatten

        // We need to use a switch statement here because we need
        // to know the type of context at runtime
        var messages = request.ContextInfo switch
        {
            GenerateTextContextInfoDto g => GetPromptMessages(g, prompt, novel, prose, records),
            SummarizeTextContextInfoDto s => GetPromptMessages(s, prompt, novel, prose, records),
            ReplaceTextContextInfoDto r => GetPromptMessages(r, prompt, novel, prose, records),
            CreateCompendiumRecordContextInfoDto c => GetPromptMessages(c, prompt, novel, prose, records),
            EditCompendiumRecordContextInfoDto e => GetPromptMessages(e, prompt, novel, prose, records),
            _ => throw new NotImplementedException("Unknown context type.")
        };
        
        _logger.LogInformation("Sending prompt with messages: {@Messages}", messages);
        
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
                // TODO: Find a better way to do this...
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
                _ => throw new NotImplementedException("Unknown context type.")
            }
        });
    }
}
