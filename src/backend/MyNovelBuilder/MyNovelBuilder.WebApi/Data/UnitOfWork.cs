using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Interfaces;

namespace MyNovelBuilder.WebApi.Data;

/// <summary>
/// Implementation of the Unit of Work pattern.
/// </summary>
public class UnitOfWork : IUnitOfWork, IDisposable, IAsyncDisposable
{
    private bool _disposed;
    private readonly Dictionary<Type, object> _repositories = new();
    
    /// <inheritdoc />
    public AppDbContext DbContext { get; }

    /// <summary></summary>
    public UnitOfWork(AppDbContext dbContext)
    {
        DbContext = dbContext;
    }

    /// <inheritdoc />
    public IRepository<T> GetRepository<T>() where T : Entity
    {
        if (_repositories.ContainsKey(typeof(T)))
        {
            return (IRepository<T>) _repositories[typeof(T)];
        }

        var repository = new Repository<T>(DbContext);
        _repositories.Add(typeof(T), repository);
        return repository;
    }

    /// <inheritdoc />
    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return DbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task ReloadAsync<T>(T entity, CancellationToken cancellationToken = default) where T : Entity
    {
        return DbContext.Entry(entity).ReloadAsync(cancellationToken);
    }

    /// <summary>
    /// Dispose the database context.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            DbContext.Dispose();
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
        await DbContext.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
