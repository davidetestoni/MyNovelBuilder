using MyNovelBuilder.WebApi.Data.Repositories;

namespace MyNovelBuilder.WebApi.Data;

/// <summary>
/// Implementation of the Unit of Work pattern.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

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
}
