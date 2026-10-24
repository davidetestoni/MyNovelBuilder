using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Models.AudioGeneration;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for immersive multi-speaker TTS planning and playback.
/// </summary>
public interface IImmersiveTtsService
{
    /// <summary>
    /// Prepare and resolve the immersive chunk plan in debug form.
    /// </summary>
    Task<ImmersiveTtsDebugResponseDto> PrepareDebugAsync(
        ImmersiveTtsRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>Inspect a plan using frozen audiobook inputs.</summary>
    Task<ImmersiveTtsDebugResponseDto> PrepareDebugAsync(AudiobookRenderRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate a continuous WAV stream for immersive multi-speaker playback.
    /// </summary>
    Task<Stream> GenerateStreamAsync(
        ImmersiveTtsRequestDto request,
        CancellationToken cancellationToken = default);
    /// <summary>Render a section using frozen audiobook inputs.</summary>
    Task<Stream> GenerateStreamAsync(AudiobookRenderRequest request, CancellationToken cancellationToken = default);

}
