using Microsoft.EntityFrameworkCore;
using MyNovelBuilder.WebApi.Data.Entities;

namespace MyNovelBuilder.WebApi.Data.Repositories;

/// <summary>
/// Repository for novels.
/// </summary>
public class NovelRepository : Repository<Novel>, INovelRepository
{
    /// <summary></summary>
    public NovelRepository(AppDbContext context) : base(context)
    {
    }

    /// <inheritdoc />
    public async Task<Novel?> GetWithReferencesByIdAsync(Guid id)
    {
        return await Context.Set<Novel>()
            .Include(n => n.MainCharacter)
            .Include(n => n.Compendia)
            .FirstOrDefaultAsync(n => n.Id == id);
    }
}
