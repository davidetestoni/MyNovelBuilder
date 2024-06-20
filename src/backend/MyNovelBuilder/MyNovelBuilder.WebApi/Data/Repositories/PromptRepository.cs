using Microsoft.EntityFrameworkCore;
using MyNovelBuilder.WebApi.Data.Entities;

namespace MyNovelBuilder.WebApi.Data.Repositories;

/// <summary>
/// Repository for prompts.
/// </summary>
public class PromptRepository : Repository<Prompt>, IPromptRepository
{
    /// <summary></summary>
    public PromptRepository(AppDbContext context) : base(context)
    {
    }

    /// <inheritdoc />
    public async Task<Prompt?> GetWithMessagesByIdAsync(Guid id)
    {
        return await Context.Set<Prompt>()
            .Include(p => p.Messages)
            .FirstOrDefaultAsync(p => p.Id == id);
    }
}
