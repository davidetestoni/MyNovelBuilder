using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Models.Prompts;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Creates processed prompts for the world-building agent.
/// </summary>
public interface IWorldBuildingPromptCreatorService
{
    /// <summary>
    /// Create a processed prompt from a prompt and context.
    /// </summary>
    Task<ProcessedPrompt> CreatePromptAsync(
        Prompt prompt,
        WorldBuildingAgentContextInfoDto context,
        CancellationToken cancellationToken = default);
}
