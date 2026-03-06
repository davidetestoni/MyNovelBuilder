using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Models.MediaLibrary;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for linked media folders.
/// </summary>
public interface IMediaFolderService
{
    /// <summary>
    /// Get a media folder by its ID.
    /// </summary>
    Task<MediaFolder> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all linked media folders.
    /// </summary>
    Task<IEnumerable<MediaFolder>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new linked media folder.
    /// </summary>
    Task CreateAsync(MediaFolder mediaFolder, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a linked media folder by its ID.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upload a media file into the linked folder.
    /// </summary>
    Task<MediaFileInfo> UploadMediaAsync(
        Guid id,
        string fileName,
        IFormFile file,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a media file from the linked folder.
    /// </summary>
    Task DeleteMediaAsync(Guid id, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all top-level media files from the linked folder.
    /// </summary>
    Task<IEnumerable<MediaFileInfo>> GetAllMediaAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the content of a media file from the linked folder.
    /// </summary>
    Task<MediaFileContent> GetMediaAsync(Guid id, string fileName, CancellationToken cancellationToken = default);
}
