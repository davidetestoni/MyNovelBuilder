using MyNovelBuilder.WebApi.Data.Entities;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for compendium records.
/// </summary>
public interface ICompendiumRecordService
{
    /// <summary>
    /// Get a compendium record by its ID.
    /// </summary>
    Task<CompendiumRecord> GetByIdAsync(Guid id);
    
    /// <summary>
    /// Get all compendium records.
    /// </summary>
    Task<IEnumerable<CompendiumRecord>> GetAllAsync();
    
    /// <summary>
    /// Create a compendium record.
    /// </summary>
    Task CreateAsync(CompendiumRecord compendiumRecord);
    
    /// <summary>
    /// Update a compendium record.
    /// </summary>
    Task UpdateAsync(CompendiumRecord compendiumRecord);
    
    /// <summary>
    /// Delete a compendium record by its ID.
    /// </summary>
    Task DeleteAsync(Guid id);
}
