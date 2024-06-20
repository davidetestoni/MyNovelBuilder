using MyNovelBuilder.WebApi.Data.Entities;

namespace MyNovelBuilder.WebApi.Data.Repositories;

/// <summary>
/// Repository for novels.
/// </summary>
public interface INovelRepository : IRepository<Novel>
{
    /// <summary>
    /// Get a novel by its ID, including its references.
    /// </summary>
    Task<Novel?> GetWithReferencesByIdAsync(Guid id);
}
