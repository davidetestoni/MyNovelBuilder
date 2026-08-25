using MyNovelBuilder.WebApi.Data.Entities;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for voices.
/// </summary>
public interface IVoiceService
{
    /// <summary>
    /// Get a voice by its ID.
    /// </summary>
    Task<Voice> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all voices.
    /// </summary>
    Task<IEnumerable<Voice>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a voice with its WAV sample.
    /// </summary>
    Task CreateAsync(Voice voice, IFormFile wavFile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update a voice and optionally replace its WAV sample.
    /// </summary>
    Task UpdateAsync(Voice voice, IFormFile? wavFile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a voice by its ID.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a preview of a voice sample WAV file with a maximum duration.
    /// </summary>
    Task<byte[]> GetPreviewAsync(
        Guid id,
        int previewSeconds = 5,
        CancellationToken cancellationToken = default);
}
