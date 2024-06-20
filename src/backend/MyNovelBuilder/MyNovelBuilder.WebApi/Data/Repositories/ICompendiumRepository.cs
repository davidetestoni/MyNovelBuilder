using MyNovelBuilder.WebApi.Data.Entities;

namespace MyNovelBuilder.WebApi.Data.Repositories;

/// <summary>
/// Repository for compendiums.
/// </summary>
public interface ICompendiumRepository : IRepository<Compendium>
{
    /// <summary>
    /// Get a compendium by its ID, including its records.
    /// </summary>
    Task<Compendium?> GetWithRecordsByIdAsync(Guid id);
}
