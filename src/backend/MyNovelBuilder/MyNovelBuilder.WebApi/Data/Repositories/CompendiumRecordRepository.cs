using Microsoft.EntityFrameworkCore;
using MyNovelBuilder.WebApi.Data.Entities;

namespace MyNovelBuilder.WebApi.Data.Repositories;

/// <summary>
/// Repository for compendium records.
/// </summary>
public class CompendiumRecordRepository : Repository<CompendiumRecord>, ICompendiumRecordRepository
{
    /// <summary></summary>
    public CompendiumRecordRepository(AppDbContext context) : base(context)
    {
    }

    /// <inheritdoc />
    public async Task<CompendiumRecord?> GetWithCompendiumByIdAsync(Guid id)
    {
        return await Context.Set<CompendiumRecord>()
            .Include(cr => cr.Compendium)
            .FirstOrDefaultAsync(cr => cr.Id == id);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<CompendiumRecord>> GetByCompendiumIdAsync(Guid compendiumId)
    {
        return await Context.Set<CompendiumRecord>()
            .Where(cr => cr.Compendium.Id == compendiumId)
            .ToListAsync();
    }
}
