using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Models.Prompts;
using MyNovelBuilder.WebApi.Models.Tts;
using MyNovelBuilder.WebApi.Services.TextGeneration;

namespace MyNovelBuilder.WebApi.Services.Tts;

/// <summary>
/// Service for generating audio using TTS.
/// </summary>
public interface ITtsService
{
    /// <summary>
    /// The audio format of the generated output.
    /// </summary>
    AudioFormat OutputAudioFormat { get; }

    /// <summary>
    /// Indicates whether the specified model supports text emphasis tags.
    /// </summary>
    bool SupportsTextEmphasis(string? modelId) => false;

    /// <summary>
    /// Indicates whether this TTS service supports voice design.
    /// </summary>
    bool SupportsVoiceDesign() => false;

    /// <summary>
    /// Emphasize the given text for this TTS service.
    /// Services that do not support emphasis should return the original text unchanged.
    /// </summary>
    Task<string> EmphasizeTextAsync(
        TtsRequest request,
        Func<CancellationToken, ValueTask<ITextGenerationService>> textGenerationServiceFactory,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(request.Message);
    }

    /// <summary>
    /// Generate a WAV sample from a voice design prompt and voice description.
    /// Services that do not support voice design should throw <see cref="NotSupportedException" />.
    /// </summary>
    Task<byte[]> VoiceDesignAsync(
        string prompt,
        WritingLanguage language,
        string voiceDescription,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Voice design is not supported by this TTS service.");
    }
    
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
