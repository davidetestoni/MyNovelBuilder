using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Models.Images;

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
    /// Get the gallery images for a compendium record.
    /// </summary>
    Task<IEnumerable<ImageRef>> GetGalleryImagesAsync(Guid id);
    
    /// <summary>
    /// Upload an image for a compendium record.
    /// </summary>
    Task UploadImageAsync(Guid id, IFormFile file, bool isCurrent = false);
    
    /// <summary>
    /// Set an image as the main image for a compendium record.
    /// </summary>
    Task SetMainImageAsync(Guid id, Guid imageId);
    
    /// <summary>
    /// Delete an image for a compendium record.
    /// </summary>
    Task DeleteImageAsync(Guid id, Guid imageId);
}
