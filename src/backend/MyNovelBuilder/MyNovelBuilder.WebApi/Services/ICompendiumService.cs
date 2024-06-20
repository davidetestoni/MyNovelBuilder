using MyNovelBuilder.WebApi.Data.Entities;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for compendiums.
/// </summary>
public interface ICompendiumService
{
    /// <summary>
    /// Get a compendium by its ID.
    /// </summary>
    Task<Compendium> GetByIdAsync(Guid id);
    
    /// <summary>
    /// Get all compendiums.
    /// </summary>
    Task<IEnumerable<Compendium>> GetAllAsync();
    
    /// <summary>
    /// Create a compendium.
    /// </summary>
    Task CreateAsync(Compendium compendium);
    
    /// <summary>
    /// Update a compendium.
    /// </summary>
    Task UpdateAsync(Compendium compendium);
    
    /// <summary>
    /// Delete a compendium by its ID.
    /// </summary>
    Task DeleteAsync(Guid id);
}
