using Microsoft.EntityFrameworkCore;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Interfaces;

namespace MyNovelBuilder.WebApi.Data;

/// <summary>
/// Base generic repository for CRUD operations.
/// </summary>
public class Repository<T> : IRepository<T>, IDisposable, IAsyncDisposable where T : Entity
{
    private bool _disposed;
    private readonly AppDbContext _dbContext;
    private readonly DbSet<T> _dbSet;
    
    /// <summary></summary>
    public Repository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
        _dbSet = _dbContext.Set<T>();
    }
    
    /// <summary>
    /// Dispose the database context.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _dbContext.Dispose();
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
        await _dbContext.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public async Task<T?> GetByIdAsync(Guid id)
    {
        return await _dbSet.FindAsync(id);
    }

    /// <inheritdoc />
    public IQueryable<T> GetQueryable()
    {
        return _dbSet.AsQueryable();
    }

    /// <inheritdoc />
    public void Add(T entity)
    {
        _dbSet.Add(entity);
    }

    /// <inheritdoc />
    public void AddRange(IEnumerable<T> entities)
    {
        _dbSet.AddRange(entities);
    }

    /// <inheritdoc />
    public void Remove(T entity)
    {
        _dbSet.Remove(entity);
    }

    /// <inheritdoc />
    public void Update(T entity)
    {
        _dbSet.Update(entity);
    }
}
