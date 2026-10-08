using MyNovelBuilder.WebApi.Models.Tts;
namespace MyNovelBuilder.WebApi.Services;

/// <summary>Finite synthesis with persistent chunk checkpoints.</summary>
public interface IAudiobookTtsService
{
    /// <summary>Renders frozen speech and returns a completed artifact key.</summary>
    Task<string> GenerateArtifactAsync(TextToSpeechGenerationRequest request, CancellationToken cancellationToken = default);
}
