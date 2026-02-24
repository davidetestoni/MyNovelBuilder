using MyNovelBuilder.WebApi.Models.AudioGeneration;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Interface for audio repository to store and retrieve generated audio files based on generation parameters.
/// </summary>
public interface IAudioRepository
{
    /// <summary>
    /// Retrieves the audio file for the given parameters if it exists in the repository.
    /// Returns null if the audio file does not exist, allowing the caller to generate it if needed.
    /// </summary>
    Task<byte[]>? GetAudioFileAsync(AudioGenerationParameters parameters, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Saves the generated audio file to the repository with a unique identifier based on the parameters.
    /// </summary>
    Task SaveAudioFileAsync(AudioGenerationParameters parameters, byte[] audioData, CancellationToken cancellationToken = default);
}
