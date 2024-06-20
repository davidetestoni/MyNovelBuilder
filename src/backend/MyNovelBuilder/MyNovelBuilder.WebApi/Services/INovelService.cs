using MyNovelBuilder.WebApi.Data.Entities;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for novels.
/// </summary>
public interface INovelService
{
    /// <summary>
    /// Get a novel by its ID.
    /// </summary>
    Task<Novel> GetByIdAsync(Guid id);
    
    /// <summary>
    /// Get all novels.
    /// </summary>
    Task<IEnumerable<Novel>> GetAllAsync();
    
    /// <summary>
    /// Create a novel.
    /// </summary>
    Task CreateAsync(Novel novel);
    
    /// <summary>
    /// Update a novel.
    /// </summary>
    Task UpdateAsync(Novel novel);
    
    /// <summary>
    /// Delete a novel by its ID.
    /// </summary>
    Task DeleteAsync(Guid id);
}
