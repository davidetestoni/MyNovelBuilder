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
    Task<Novel> GetByIdAsync(Guid id);
    
    /// <summary>
    /// Get all novels.
    /// </summary>
    Task<IEnumerable<Novel>> GetAllAsync();
    
    /// <summary>
    /// Create a novel.
    /// </summary>
    Task CreateAsync(Novel novel);
    
    /// <summary>
    /// Update a novel.
    /// </summary>
    Task UpdateAsync(Novel novel);
    
    /// <summary>
    /// Delete a novel by its ID.
    /// </summary>
    Task DeleteAsync(Guid id);
    
    /// <summary>
    /// Get the prose for a novel.
    /// </summary>
    Task<Prose> GetProseAsync(Guid id);
    
    /// <summary>
    /// Update the prose for a novel.
    /// </summary>
    Task UpdateProseAsync(Guid id, Prose prose);

    /// <summary>
    /// Get the cover image location for a novel.
    /// </summary>
    string? GetCoverImageLocation(Guid id);
    
    /// <summary>
    /// Upload a cover image for a novel.
    /// </summary>
    Task UploadCoverImageAsync(Guid id, IFormFile file);
    
    /// <summary>
    /// Delete a cover image for a novel.
    /// </summary>
    void DeleteCoverImage(Guid id);
}
