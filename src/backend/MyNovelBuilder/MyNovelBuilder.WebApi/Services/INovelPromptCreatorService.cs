using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Dtos.Prompt;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for creating novel-scoped prompts.
/// </summary>
public interface INovelPromptCreatorService
{
    /// <summary>
    /// Create a prompt based on the request.
    /// </summary>
    Task<IEnumerable<PromptMessageDto>> CreatePromptAsync(GenerateTextRequestDto request);
}
