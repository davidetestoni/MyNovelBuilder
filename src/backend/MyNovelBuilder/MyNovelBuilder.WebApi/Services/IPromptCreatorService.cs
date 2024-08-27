using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Dtos.Prompt;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for creating prompts.
/// </summary>
public interface IPromptCreatorService
{
    /// <summary>
    /// Create a prompt based on the request.
    /// </summary>
    Task<IEnumerable<PromptMessageDto>> CreatePromptAsync(GenerateTextRequestDto request);
}
