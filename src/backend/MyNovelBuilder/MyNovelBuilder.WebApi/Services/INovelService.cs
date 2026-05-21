using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Models.Novels;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for novels.
/// </summary>
public interface INovelService
{
    /// <summary>
    /// Get a novel by its ID.
    /// </summary>
    Task<Novel> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all novels.
    /// </summary>
    Task<IEnumerable<Novel>> GetAllAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create a novel.
    /// </summary>
    Task CreateAsync(Novel novel, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update a novel.
    /// </summary>
    Task UpdateAsync(Novel novel, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete a novel by its ID.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get the prose for a novel.
    /// </summary>
    Task<Prose> GetProseAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update the prose for a novel.
    /// </summary>
    Task UpdateProseAsync(Guid id, Prose prose, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the cover image location for a novel.
    /// </summary>
    string? GetCoverImageLocation(Guid id);
    
    /// <summary>
    /// Upload a cover image for a novel.
    /// </summary>
    Task UploadCoverImageAsync(Guid id, IFormFile file, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete a cover image for a novel.
    /// </summary>
    void DeleteCoverImage(Guid id);

    /// <summary>
    /// Upload prose media for a novel.
    /// Returns the filename of the uploaded media file.
    /// </summary>
    Task<string> UploadProseImageAsync(Guid id, IFormFile file, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a prose media file for a novel by file name.
    /// </summary>
    Task DeleteProseImageAsync(Guid id, string fileName, CancellationToken cancellationToken = default);
}
