using MyNovelBuilder.WebApi.Data.Entities;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for prompts.
/// </summary>
public interface IPromptService
{
    /// <summary>
    /// Get a prompt by its ID.
    /// </summary>
    Task<Prompt> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all prompts.
    /// </summary>
    Task<IEnumerable<Prompt>> GetAllAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create a prompt.
    /// </summary>
    Task CreateAsync(Prompt prompt, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update a prompt.
    /// </summary>
    Task UpdateAsync(Prompt prompt, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete a prompt by its ID.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
