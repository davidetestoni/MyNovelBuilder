using MyNovelBuilder.WebApi.Data.Repositories;

namespace MyNovelBuilder.WebApi.Data;

/// <summary>
/// Implementation of the Unit of Work pattern.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    /// <summary></summary>
    public UnitOfWork(AppDbContext context)
    {
        _context = context;
        Novels = new NovelRepository(_context);
        Compendia = new CompendiumRepository(_context);
        CompendiumRecords = new CompendiumRecordRepository(_context);
        Prompts = new PromptRepository(_context);
        Voices = new VoiceRepository(_context);
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
    public IVoiceRepository Voices { get; }

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
