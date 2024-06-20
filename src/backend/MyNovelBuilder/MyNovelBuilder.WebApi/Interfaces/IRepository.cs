using MyNovelBuilder.WebApi.Data.Entities;

namespace MyNovelBuilder.WebApi.Interfaces;

/// <summary>
/// Base repository for all entities.
/// </summary>
public interface IRepository<T> where T : Entity
{
    /// <summary>
    /// Get an entity by its ID. Returns null if not found.
    /// </summary>
    Task<T?> GetByIdAsync(Guid id);
    
    /// <summary>
    /// Get queryable for the entity.
    /// </summary>
    IQueryable<T> GetQueryable();
    
    /// <summary>
    /// Add an entity.
    /// </summary>
    void Add(T entity);
    
    /// <summary>
    /// Add a range of entities.
    /// </summary>
    void AddRange(IEnumerable<T> entities);
    
    /// <summary>
    /// Remove an entity.
    /// </summary>
    void Remove(T entity);
    
    /// <summary>
    /// Update an entity.
    /// </summary>
    void Update(T entity);
}
