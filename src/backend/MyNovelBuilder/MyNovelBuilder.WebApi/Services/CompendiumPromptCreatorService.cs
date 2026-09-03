using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Models.Novels;
using MyNovelBuilder.WebApi.Models.Prompts;
using MyNovelBuilder.WebApi.Prompts.Builders;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for creating compendium-scoped prompts.
/// </summary>
public class CompendiumPromptCreatorService : ICompendiumPromptCreatorService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<CompendiumPromptCreatorService> _logger;

    /// <summary></summary>
    public CompendiumPromptCreatorService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<CompendiumPromptCreatorService> logger)
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

        var prompt = await unitOfWork.Prompts.GetByIdAsync(request.PromptId, cancellationToken);
        if (prompt is null)
        {
            throw new ApiException(ErrorCodes.PromptNotFound,
                $"Prompt with ID {request.PromptId} not found.");
        }

        if (request.ContextInfo is not CompendiumTextGenerationContextInfoDto compendiumContextInfo)
        {
            throw new ApiException(ErrorCodes.InvalidPromptContext,
                "The prompt context is invalid.");
        }

        var requiredContextType = prompt.Type switch
        {
            PromptType.DescribeCompendiumImage => typeof(DescribeCompendiumImageContextInfoDto),
            PromptType.CreateCompendiumRecordImageGenerationPrompt =>
                typeof(CreateCompendiumRecordImageGenerationPromptContextInfoDto),
            _ => throw new ApiException(ErrorCodes.InvalidPromptContext,
                "This prompt type is not valid for compendium prompt generation.")
        };
        
        if (request.ContextInfo.GetType() != requiredContextType)
        {
            throw new ApiException(ErrorCodes.InvalidPromptContext,
                "The prompt context is invalid.");
        }

        var compendium = await unitOfWork.Compendia.GetWithRecordsByIdAsync(compendiumContextInfo.CompendiumId, cancellationToken);
        if (compendium is null)
        {
            throw new ApiException(ErrorCodes.CompendiumNotFound,
                $"Compendium with ID {compendiumContextInfo.CompendiumId} not found.");
        }

        var processedPrompt = request.ContextInfo switch
        {
            DescribeCompendiumImageContextInfoDto d => ProcessPrompt(d, prompt, compendium.Records.ToList()),
            CreateCompendiumRecordImageGenerationPromptContextInfoDto c =>
                ProcessPrompt(c, prompt, compendium.Records.ToList()),
            _ => throw new ApiException(ErrorCodes.InvalidPromptContext,
                "The prompt context is invalid.")
        };

        _logger.LogDebug(
            "Sending compendium prompt with messages for compendium {CompendiumId}: {@Messages}",
            compendiumContextInfo.CompendiumId, processedPrompt.Messages);

        return processedPrompt;
    }

    private static ProcessedPrompt ProcessPrompt<T>(
        T clientContext,
        Prompt prompt,
        List<CompendiumRecord> records)
        where T : CompendiumTextGenerationContextInfoDto
    {
        var includedCompendiumRecordIds = new HashSet<Guid>();
        var messages = prompt.Messages.Select(message => new PromptMessage
        {
            Role = message.Role,
            Message = clientContext switch
            {
                DescribeCompendiumImageContextInfoDto d => new DescribeCompendiumImagePromptBuilder(message.Message)
                    .ReplacePlaceholders(new CompendiumPromptBuilderContext<DescribeCompendiumImageContextInfoDto>
                    {
                        Client = d,
                        CompendiumRecords = records,
                        IncludedCompendiumRecordIds = includedCompendiumRecordIds
                    }).ToString(),
                CreateCompendiumRecordImageGenerationPromptContextInfoDto c =>
                    new CreateCompendiumRecordImageGenerationPromptBuilder(message.Message)
                        .ReplacePlaceholders(
                            new CompendiumPromptBuilderContext<CreateCompendiumRecordImageGenerationPromptContextInfoDto>
                            {
                                Client = c,
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
