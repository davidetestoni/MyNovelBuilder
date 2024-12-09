using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Models.Media;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for compendium records.
/// </summary>
public interface ICompendiumRecordService
{
    /// <summary>
    /// Get a compendium record by its ID.
    /// </summary>
    Task<CompendiumRecord> GetByIdAsync(Guid id);
    
    /// <summary>
    /// Get all compendium records by compendium ID.
    /// </summary>
    Task<IEnumerable<CompendiumRecord>> GetByCompendiumIdAsync(Guid compendiumId);
    
    /// <summary>
    /// Get all compendium records.
    /// </summary>
    Task<IEnumerable<CompendiumRecord>> GetAllAsync();
    
    /// <summary>
    /// Create a compendium record.
    /// </summary>
    Task CreateAsync(CompendiumRecord compendiumRecord);
    
    /// <summary>
    /// Update a compendium record.
    /// </summary>
    Task UpdateAsync(CompendiumRecord compendiumRecord);
    
    /// <summary>
    /// Delete a compendium record by its ID.
    /// </summary>
    Task DeleteAsync(Guid id);
    
    /// <summary>
    /// Get the gallery media for a compendium record.
    /// </summary>
    Task<IEnumerable<MediaRef>> GetGalleryMediaAsync(Guid id);
    
    /// <summary>
    /// Upload a media for a compendium record.
    /// </summary>
    Task UploadMediaAsync(Guid id, IFormFile file, bool isCurrent = false);
    
    /// <summary>
    /// Set an image as the current image for a compendium record.
    /// </summary>
    Task SetCurrentImageAsync(Guid id, Guid imageId);
    
    /// <summary>
    /// Delete a media for a compendium record.
    /// </summary>
    Task DeleteMediaAsync(Guid id, Guid mediaId);
}
