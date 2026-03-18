using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Models.Prompts;
using MyNovelBuilder.WebApi.Prompts.Builders;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for creating prompts that are not scoped to a novel or compendium.
/// </summary>
public class GenericPromptCreatorService : IGenericPromptCreatorService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<GenericPromptCreatorService> _logger;

    /// <summary></summary>
    public GenericPromptCreatorService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<GenericPromptCreatorService> logger)
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

        if (request.ContextInfo is not GenericTextGenerationContextInfoDto genericContextInfo)
        {
            throw new ApiException(ErrorCodes.InvalidPromptContext,
                "The prompt context is invalid.");
        }

        var requiredContextType = prompt.Type switch
        {
            PromptType.DescribeImage => typeof(DescribeImageContextInfoDto),
            _ => throw new ApiException(ErrorCodes.InvalidPromptContext,
                "This prompt type is not valid for generic prompt generation.")
        };

        if (request.ContextInfo.GetType() != requiredContextType)
        {
            throw new ApiException(ErrorCodes.InvalidPromptContext,
                "The prompt context is invalid.");
        }

        var processedPrompt = genericContextInfo switch
        {
            DescribeImageContextInfoDto d => ProcessPrompt(d, prompt),
            _ => throw new ApiException(ErrorCodes.InvalidPromptContext,
                "The prompt context is invalid.")
        };

        _logger.LogInformation("Sending generic prompt with messages: {@Messages}", processedPrompt.Messages);
        return processedPrompt;
    }

    private static ProcessedPrompt ProcessPrompt(
        DescribeImageContextInfoDto clientContext,
        Prompt prompt)
    {
        var messages = prompt.Messages.Select(message => new PromptMessage
        {
            Role = message.Role,
            Message = new DescribeImagePromptBuilder(message.Message)
                .ReplacePlaceholders(new GenericPromptBuilderContext<DescribeImageContextInfoDto>
                {
                    Client = clientContext
                }).ToString()
        }).ToList();

        return new ProcessedPrompt
        {
            Messages = messages,
            IncludedCompendiumRecordIds = []
        };
    }
}
