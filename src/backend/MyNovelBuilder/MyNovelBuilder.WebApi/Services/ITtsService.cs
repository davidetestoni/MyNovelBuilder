using MyNovelBuilder.WebApi.Dtos.Generate;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for generating audio using TTS.
/// </summary>
public interface ITtsService
{
    /// <summary>
    /// Generate (usually MP3) audio bytes from the given text.
    /// </summary>
    Task<byte[]> GenerateAudioAsync(TtsRequestDto request);
    
    /// <summary>
    /// Get a list of available voices.
    /// </summary>
    Task<IEnumerable<TtsVoiceDto>> GetVoicesAsync();
}
