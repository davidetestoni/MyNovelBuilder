using MyNovelBuilder.WebApi.Models.Tts;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Shared service for generating WAV audio from TTS requests.
/// </summary>
public interface ITtsAudioGenerationService
{
    /// <summary>
    /// Generate WAV audio bytes for the request, using cache when available.
    /// </summary>
    Task<byte[]> GenerateWavBytesAsync(
        TextToSpeechGenerationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate a WAV stream for the request, using cache when available.
    /// </summary>
    Task<Stream> GenerateWavStreamAsync(
        TextToSpeechGenerationRequest request,
        CancellationToken cancellationToken = default);
}
