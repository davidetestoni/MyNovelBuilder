using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Models.Tts;

namespace MyNovelBuilder.WebApi.Services.Tts;

/// <summary>
/// Service for generating audio using TTS.
/// </summary>
public interface ITtsService
{
    /// <summary>
    /// Indicates if the TTS service supports emphasis tags in the text for the given voice/model.
    /// </summary>
    bool SupportsEmphasisTags(string? modelId, string voiceId);
    
    /// <summary>
    /// The audio format of the generated output.
    /// </summary>
    AudioFormat OutputAudioFormat { get; }
    
    /// <summary>
    /// Generate audio bytes from the given text.
    /// Returns the entire audio as a byte array.
    /// </summary>
    Task<byte[]> GenerateAudioAsync(TtsRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generate audio stream from the given text.
    /// Returns the audio as an encoded stream.
    /// </summary>
    Task<Stream> GenerateAudioStreamAsync(TtsRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a list of available models and voices.
    /// </summary>
    Task<IEnumerable<TtsModelDto>> GetModelsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the current provider's USD balance, if available.
    /// </summary>
    Task<decimal?> GetBalanceUsdAsync(CancellationToken cancellationToken = default);
}
