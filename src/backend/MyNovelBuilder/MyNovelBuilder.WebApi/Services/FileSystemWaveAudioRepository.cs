using Microsoft.Extensions.Options;
using MyNovelBuilder.WebApi.Models.AudioGeneration;
using MyNovelBuilder.WebApi.Options;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Local implementation of IAudioRepository that stores audio files on the local disk.
/// </summary>
public class FileSystemWaveAudioRepository : IAudioRepository
{
    private readonly string _audioFolder;

    /// <summary></summary>
    public FileSystemWaveAudioRepository(IOptions<AppStorageOptions> storageOptions)
    {
        _audioFolder = Path.Combine(storageOptions.Value.DataFolder, "audio");
    }

    private string GetAudioPath(AudioGenerationParameters parameters)
    {
        var hash = parameters.GetHash();
        var safeHash = hash
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        return Path.Combine(_audioFolder, $"{safeHash}.wav");
    }

    /// <inheritdoc />
    public Task<byte[]>? GetAudioFileAsync(AudioGenerationParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var path = GetAudioPath(parameters);

        return File.Exists(path)
            ? File.ReadAllBytesAsync(path, cancellationToken)
            : null;
    }

    /// <inheritdoc />
    public async Task SaveAudioFileAsync(AudioGenerationParameters parameters,
        byte[] audioData,
        CancellationToken cancellationToken = default)
    {
        var path = GetAudioPath(parameters);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, audioData, cancellationToken);
    }
}
