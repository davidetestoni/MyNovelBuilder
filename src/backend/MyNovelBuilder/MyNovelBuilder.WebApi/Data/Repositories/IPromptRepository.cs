using MyNovelBuilder.WebApi.Data.Entities;

namespace MyNovelBuilder.WebApi.Data.Repositories;

/// <summary>
/// Repository for prompts.
/// </summary>
public interface IPromptRepository : IRepository<Prompt>
{
    /// <summary>
    /// Get a prompt by its ID, including its messages.
    /// </summary>
    Task<Prompt?> GetWithMessagesByIdAsync(Guid id);
}
