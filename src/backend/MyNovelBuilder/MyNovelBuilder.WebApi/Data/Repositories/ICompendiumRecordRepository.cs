using MyNovelBuilder.WebApi.Data.Entities;

namespace MyNovelBuilder.WebApi.Data.Repositories;

/// <summary>
/// Repository for compendium records.
/// </summary>
public interface ICompendiumRecordRepository : IRepository<CompendiumRecord>
{
    /// <summary>
    /// Get a compendium record by its ID, including its compendium.
    /// </summary>
    Task<CompendiumRecord?> GetWithCompendiumByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all compendium records for a compendium.
    /// </summary>
    Task<IEnumerable<CompendiumRecord>> GetByCompendiumIdAsync(
        Guid compendiumId,
        CancellationToken cancellationToken = default);
}
