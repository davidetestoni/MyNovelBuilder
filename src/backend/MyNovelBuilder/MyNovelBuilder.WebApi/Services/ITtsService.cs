using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for generating audio using TTS.
/// </summary>
public interface ITtsService
{
    /// <summary>
    /// Indicates if the TTS service supports emphasis tags in the text.
    /// </summary>
    bool SupportsEmphasisTags { get; }
    
    /// <summary>
    /// The audio format of the generated output.
    /// </summary>
    AudioFormat OutputAudioFormat { get; }
    
    /// <summary>
    /// Generate audio bytes from the given text.
    /// Returns the entire audio as a byte array.
    /// </summary>
    Task<byte[]> GenerateAudioAsync(TtsRequestDto request);
    
    /// <summary>
    /// Generate audio stream from the given text.
    /// Returns the audio as an encoded stream.
    /// </summary>
    Task<Stream> GenerateAudioStreamAsync(TtsRequestDto request);
    
    /// <summary>
    /// Get a list of available voices.
    /// </summary>
    Task<IEnumerable<TtsVoiceDto>> GetVoicesAsync();
}
