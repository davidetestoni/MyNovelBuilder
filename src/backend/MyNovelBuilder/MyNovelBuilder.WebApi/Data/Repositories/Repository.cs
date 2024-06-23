using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Data.Repositories;

namespace MyNovelBuilder.WebApi.Data.Repositories;

/// <summary>
/// Base repository for all entities.
/// </summary>
public class Repository<TEntity> :
    IRepository<TEntity>, IDisposable, IAsyncDisposable where TEntity : Entity
{
    /// <summary>
    /// The database context.
    /// </summary>
    protected readonly AppDbContext Context;
    
    private bool _disposed;

    /// <summary></summary>
    public Repository(AppDbContext context)
    {
        Context = context;
    }

    /// <summary>
    /// Check if an entity with a given ID exists.
    /// </summary>
    public async Task<bool> ExistsAsync(Guid id)
    {
        return await Context.Set<TEntity>().AnyAsync(e => e.Id == id);
    }
    
    /// <inheritdoc />
    public async Task<TEntity?> GetByIdAsync(Guid id)
    {
        return await Context.Set<TEntity>().FindAsync(id);
    }
    
    /// <inheritdoc />
    public async Task<IEnumerable<TEntity>> GetAllAsync()
    {
        return await Context.Set<TEntity>().ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate)
    {
        return await Context.Set<TEntity>().Where(predicate).ToListAsync();
    }

    /// <inheritdoc />
    public async Task AddAsync(TEntity entity)
    {
        await Context.Set<TEntity>().AddAsync(entity);
    }

    /// <inheritdoc />
    public async Task AddRangeAsync(IEnumerable<TEntity> entities)
    {
        await Context.Set<TEntity>().AddRangeAsync(entities);
    }

    /// <inheritdoc />
    public void Update(TEntity entity)
    {
        Context.Set<TEntity>().Update(entity);
    }

    /// <inheritdoc />
    public void Remove(TEntity entity)
    {
        Context.Set<TEntity>().Remove(entity);
    }
    
    /// <summary>
    /// Dispose the database context.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            Context.Dispose();
        }
        
        _disposed = true;
    }
    
    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
