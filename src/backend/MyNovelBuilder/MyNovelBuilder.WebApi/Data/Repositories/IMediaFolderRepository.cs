using MyNovelBuilder.WebApi.Data.Entities;

namespace MyNovelBuilder.WebApi.Data.Repositories;

/// <summary>
/// Repository for media folders.
/// </summary>
public interface IMediaFolderRepository : IRepository<MediaFolder>
{
    /// <summary>
    /// Get a media folder by its normalized path.
    /// </summary>
    Task<MediaFolder?> GetByPathAsync(string path, CancellationToken cancellationToken = default);
}
