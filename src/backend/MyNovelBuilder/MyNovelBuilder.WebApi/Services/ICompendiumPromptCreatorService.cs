using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Models.Prompts;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for creating compendium-scoped prompts.
/// </summary>
public interface ICompendiumPromptCreatorService
{
    /// <summary>
    /// Create a prompt based on the request.
    /// </summary>
    Task<ProcessedPrompt> CreatePromptAsync(GenerateTextRequestDto request, CancellationToken cancellationToken = default);
}
