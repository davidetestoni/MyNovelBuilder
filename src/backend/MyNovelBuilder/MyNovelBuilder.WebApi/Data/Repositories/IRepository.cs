using System.Linq.Expressions;
using MyNovelBuilder.WebApi.Data.Entities;

namespace MyNovelBuilder.WebApi.Data.Repositories;

/// <summary>
/// Base repository for all entities.
/// </summary>
public interface IRepository<TEntity> where TEntity : Entity
{
    /// <summary>
    /// Check if an entity with a given ID exists.
    /// </summary>
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get an entity by its ID. Returns null if not found.
    /// </summary>
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all entities.
    /// </summary>
    Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Find entities based on a predicate.
    /// </summary>
    Task<IEnumerable<TEntity>> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Add an entity.
    /// </summary>
    void Add(TEntity entity);
    
    /// <summary>
    /// Add a range of entities.
    /// </summary>
    void AddRange(IEnumerable<TEntity> entities);
    
    /// <summary>
    /// Update an entity.
    /// </summary>
    void Update(TEntity entity);
    
    /// <summary>
    /// Remove an entity.
    /// </summary>
    void Remove(TEntity entity);
}
