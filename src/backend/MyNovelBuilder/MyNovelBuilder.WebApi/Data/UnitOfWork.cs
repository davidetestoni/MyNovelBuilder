using MyNovelBuilder.WebApi.Data.Repositories;

namespace MyNovelBuilder.WebApi.Data;

/// <summary>
/// Implementation of the Unit of Work pattern.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private bool _disposed;

    /// <summary></summary>
    public UnitOfWork(AppDbContext context)
    {
        _context = context;
        Novels = new NovelRepository(_context);
        Compendiums = new CompendiumRepository(_context);
    }
    
    /// <inheritdoc />
    public INovelRepository Novels { get; private set; }
    
    /// <inheritdoc />
    public ICompendiumRepository Compendiums { get; set; }

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Dispose the database context.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _context.Dispose();
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
        await _context.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
