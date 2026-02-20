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
    public UnitOfWork(AppDbContext context,
        INovelRepository novelRepository,
        ICompendiumRepository compendiumRepository,
        ICompendiumRecordRepository compendiumRecordRepository,
        IPromptRepository promptRepository)
    {
        _context = context;
        Novels = novelRepository;
        Compendia = compendiumRepository;
        CompendiumRecords = compendiumRecordRepository;
        Prompts = promptRepository;
    }
    
    /// <inheritdoc />
    public INovelRepository Novels { get; }
    
    /// <inheritdoc />
    public ICompendiumRepository Compendia { get; }

    /// <inheritdoc />
    public ICompendiumRecordRepository CompendiumRecords { get; }

    /// <inheritdoc />
    public IPromptRepository Prompts { get; }

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
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
