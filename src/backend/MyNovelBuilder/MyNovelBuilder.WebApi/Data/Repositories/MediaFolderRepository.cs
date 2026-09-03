using Microsoft.EntityFrameworkCore;
using MyNovelBuilder.WebApi.Data.Entities;

namespace MyNovelBuilder.WebApi.Data.Repositories;

/// <summary>
/// Repository for media folders.
/// </summary>
public class MediaFolderRepository : Repository<MediaFolder>, IMediaFolderRepository
{
    /// <summary></summary>
    public MediaFolderRepository(AppDbContext context) : base(context)
    {
    }

    /// <inheritdoc />
    public async Task<MediaFolder?> GetByPathAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        return await Context.Set<MediaFolder>()
            .FirstOrDefaultAsync(folder => folder.Path == path, cancellationToken);
    }
}
