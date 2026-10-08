using System.Collections.Immutable;
using MyNovelBuilder.WebApi.Models.AudioGeneration;
namespace MyNovelBuilder.WebApi.Services;

/// <summary>Shared speaker preparation for finite rendering.</summary>
public interface IAudiobookImmersivePlanner
{
    /// <summary>Validates and resolves a frozen plan without synthesizing speech.</summary>
    Task<ImmutableArray<AudiobookSpeechChunk>> PrepareAudiobookAsync(AudiobookRenderRequest request, CancellationToken cancellationToken = default);
}
