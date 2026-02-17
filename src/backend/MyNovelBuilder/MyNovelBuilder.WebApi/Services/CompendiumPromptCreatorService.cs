using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Dtos.Prompt;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Models.Novels;
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
    public async Task<IEnumerable<PromptMessageDto>> CreatePromptAsync(
        CompendiumGenerateTextRequestDto request)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var prompt = await unitOfWork.Prompts.GetByIdAsync(request.PromptId);
        if (prompt is null)
        {
            throw new ApiException(ErrorCodes.PromptNotFound,
                $"Prompt with ID {request.PromptId} not found.");
        }

        var requiredContextType = prompt.Type switch
        {
            PromptType.DescribeImage => typeof(DescribeImageContextInfoDto),
            _ => throw new ApiException(ErrorCodes.InvalidPromptContext,
                "This prompt type is not valid for compendium prompt generation.")
        };
        
        if (request.ContextInfo.GetType() != requiredContextType)
        {
            throw new ApiException(ErrorCodes.InvalidPromptContext,
                "The prompt context is invalid.");
        }

        var compendium = await unitOfWork.Compendia.GetWithRecordsByIdAsync(request.CompendiumId);
        if (compendium is null)
        {
            throw new ApiException(ErrorCodes.CompendiumNotFound,
                $"Compendium with ID {request.CompendiumId} not found.");
        }

        var messages = request.ContextInfo switch
        {
            DescribeImageContextInfoDto d => GetPromptMessages(d, prompt, compendium.Records.ToList()),
            _ => throw new ApiException(ErrorCodes.InvalidPromptContext,
                "The prompt context is invalid.")
        };

        _logger.LogInformation(
            "Sending compendium prompt with messages for compendium {CompendiumId}: {@Messages}",
            request.CompendiumId, messages);

        return messages;
    }

    private static IEnumerable<PromptMessageDto> GetPromptMessages<T>(
        T clientContext,
        Data.Entities.Prompt prompt,
        List<Data.Entities.CompendiumRecord> records)
        where T : CompendiumTextGenerationContextInfoDto
    {
        return prompt.Messages.Select(message => new PromptMessageDto
        {
            Role = message.Role,
            Message = clientContext switch
            {
                DescribeImageContextInfoDto d => new DescribeImagePromptBuilder(message.Message)
                    .ReplacePlaceholders(new PromptBuilderContext<DescribeImageContextInfoDto>
                    {
                        Client = d,
                        Novel = new Novel
                        {
                            Title = string.Empty
                        },
                        Prose = new Prose(),
                        CompendiumRecords = records
                    }).ToString(),
                _ => throw new ApiException(ErrorCodes.InvalidPromptContext,
                    "The prompt context is invalid.")
            }
        });
    }
}
