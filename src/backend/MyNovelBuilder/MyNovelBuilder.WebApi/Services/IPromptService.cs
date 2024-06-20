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
    Task<Prompt> GetByIdAsync(Guid id);
    
    /// <summary>
    /// Get all prompts.
    /// </summary>
    Task<IEnumerable<Prompt>> GetAllAsync();
    
    /// <summary>
    /// Create a prompt.
    /// </summary>
    Task CreateAsync(Prompt prompt);
    
    /// <summary>
    /// Update a prompt.
    /// </summary>
    Task UpdateAsync(Prompt prompt);
    
    /// <summary>
    /// Delete a prompt by its ID.
    /// </summary>
    Task DeleteAsync(Guid id);
}
