using System.Text;
using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Dtos.Prompt;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;

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
        
        var prompt = await unitOfWork.Prompts.GetByIdAsync(request.PromptId);
        
        if (prompt is null)
        {
            throw new ApiException(ErrorCodes.PromptNotFound,
                $"Prompt with ID {request.PromptId} not found.");
        }
        
        var novel = await unitOfWork.Novels.GetWithReferencesByIdAsync(request.NovelId);
        
        if (novel is null)
        {
            throw new ApiException(ErrorCodes.NovelNotFound,
                $"Novel with ID {request.NovelId} not found.");
        }
        
        var recordsTasks = novel.Compendia.Select(compendium =>
            unitOfWork.CompendiumRecords.GetByCompendiumIdAsync(compendium.Id));
        
        var recordsLists = await Task.WhenAll(recordsTasks);
        var records = recordsLists.SelectMany(r => r).ToList(); // Flatten
        
        var messages = prompt.Messages.Select(m => new PromptMessageDto
        {
            Role = m.Role,
            Message = ReplacePlaceholders(m.Message, request, novel, records)
        });
        
        _logger.LogInformation("Sending prompt with messages: {@Messages}", messages);
        
        return messages;
    }
    
    private static string ReplacePlaceholders(string text, GenerateTextRequestDto request,
        Novel novel, List<CompendiumRecord> records)
    {
        var sb = new StringBuilder(text);
        
        // TODO: Think of a better way to do this, maybe using C# scripting
        sb.Replace("{{novel.language}}", novel.Language.ToString());
        sb.Replace("{{novel.pov}}", CreateNovelPovString(novel));
        sb.Replace("{{context}}", request.Context ?? string.Empty);
        sb.Replace("{{instructions}}", request.Instructions ?? string.Empty);

        return sb.ToString();
    }

    private static string CreateNovelPovString(Novel novel)
    {
        var povStringBuilder = new StringBuilder("The novel is written in ");
        
        povStringBuilder.Append(novel.Pov switch
        {
            WritingPov.FirstPerson => "first person",
            WritingPov.ThirdPersonLimited => "third person (limited perspective)",
            WritingPov.ThirdPersonOmniscient => "third person (omniscient)",
            _ => throw new NotImplementedException("Unknown POV type.")
        });
        
        if (novel.MainCharacter is not null)
        {
            povStringBuilder.Append(" from the perspective of ");
            povStringBuilder.Append(novel.MainCharacter.Name);
            povStringBuilder.Append('.');
        }
        
        return povStringBuilder.ToString();
    }
}
