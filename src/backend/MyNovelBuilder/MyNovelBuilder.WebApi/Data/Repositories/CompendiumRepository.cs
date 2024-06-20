using Microsoft.EntityFrameworkCore;
using MyNovelBuilder.WebApi.Data.Entities;

namespace MyNovelBuilder.WebApi.Data.Repositories;

/// <summary>
/// Repository for compendiums.
/// </summary>
public class CompendiumRepository : Repository<Compendium>, ICompendiumRepository
{
    /// <summary></summary>
    public CompendiumRepository(AppDbContext context) : base(context)
    {
    }

    /// <inheritdoc />
    public async Task<Compendium?> GetWithRecordsByIdAsync(Guid id)
    {
        return await Context.Set<Compendium>()
            .Include(c => c.Records)
            .FirstOrDefaultAsync(c => c.Id == id);
    }
}
