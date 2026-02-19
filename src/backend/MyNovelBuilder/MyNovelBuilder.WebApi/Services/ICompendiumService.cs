using MyNovelBuilder.WebApi.Data.Entities;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for compendia.
/// </summary>
public interface ICompendiumService
{
    /// <summary>
    /// Get a compendium by its ID.
    /// </summary>
    Task<Compendium> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all compendia.
    /// </summary>
    Task<IEnumerable<Compendium>> GetAllAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create a compendium.
    /// </summary>
    Task CreateAsync(Compendium compendium, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update a compendium.
    /// </summary>
    Task UpdateAsync(Compendium compendium, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete a compendium by its ID.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
