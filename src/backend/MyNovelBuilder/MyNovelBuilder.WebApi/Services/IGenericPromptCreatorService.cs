using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Models.Prompts;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for creating prompts that are not scoped to a novel or compendium.
/// </summary>
public interface IGenericPromptCreatorService
{
    /// <summary>
    /// Create a prompt based on the request.
    /// </summary>
    Task<ProcessedPrompt> CreatePromptAsync(
        GenerateTextRequestDto request,
        CancellationToken cancellationToken = default);
}
