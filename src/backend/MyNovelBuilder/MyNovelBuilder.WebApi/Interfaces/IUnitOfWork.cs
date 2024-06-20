using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Data.Entities;

namespace MyNovelBuilder.WebApi.Interfaces;

/// <summary>
/// Interface for the Unit of Work pattern.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// The database context.
    /// </summary>
    public AppDbContext DbContext { get; }
    
    /// <summary>
    /// Get a repository for an entity.
    /// </summary>
    IRepository<T> GetRepository<T>() where T : Entity;
    
    /// <summary>
    /// Save changes to the database.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Reload an entity from the database.
    /// </summary>
    Task ReloadAsync<T>(T entity, CancellationToken cancellationToken = default) where T : Entity;
}
