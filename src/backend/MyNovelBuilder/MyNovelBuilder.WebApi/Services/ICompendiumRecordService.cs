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
    Task<CompendiumRecord> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all compendium records by compendium ID.
    /// </summary>
    Task<IEnumerable<CompendiumRecord>> GetByCompendiumIdAsync(
        Guid compendiumId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all compendium records with the provided IDs.
    /// </summary>
    Task<IEnumerable<CompendiumRecord>> GetByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all compendium records.
    /// </summary>
    Task<IEnumerable<CompendiumRecord>> GetAllAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create a compendium record.
    /// </summary>
    Task CreateAsync(CompendiumRecord compendiumRecord, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update a compendium record.
    /// </summary>
    Task UpdateAsync(CompendiumRecord compendiumRecord, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete a compendium record by its ID.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get the gallery media for a compendium record.
    /// </summary>
    Task<IEnumerable<MediaRef>> GetGalleryMediaAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Upload a media for a compendium record.
    /// </summary>
    Task UploadMediaAsync(
        Guid id,
        IFormFile file,
        bool isCurrent = false,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Set an image as the current image for a compendium record.
    /// </summary>
    Task SetCurrentImageAsync(Guid id, Guid imageId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete a media for a compendium record.
    /// </summary>
    Task DeleteMediaAsync(Guid id, Guid mediaId, CancellationToken cancellationToken = default);
}
