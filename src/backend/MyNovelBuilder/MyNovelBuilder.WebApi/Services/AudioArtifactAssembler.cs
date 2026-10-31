using Microsoft.Extensions.Options;
using MyNovelBuilder.WebApi.Helpers;
using MyNovelBuilder.WebApi.Models.AudioGeneration;
using MyNovelBuilder.WebApi.Options;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>Bounded-buffer assembly of finite 24 kHz mono 16-bit PCM WAV artifacts.</summary>
public sealed class AudioArtifactAssembler(IAudioArtifactRepository artifacts, IOptions<AppStorageOptions> storage)
{
    /// <summary>Concatenates speech units with pauses only between units, never after the last unit.</summary>
    public async Task<string> AssembleAsync(IReadOnlyList<string> keys, int pauseMs, CancellationToken cancellationToken = default)
        => await AssembleCoreAsync(keys, pauseMs, false, cancellationToken);

    /// <summary>Concatenates normalized section PCM into a cached chapter with a 500 ms gap and no trailing silence.</summary>
    public Task<string> AssembleChapterAsync(IReadOnlyList<string> keys, CancellationToken cancellationToken = default)
        => AssembleCoreAsync(keys, 500, true, cancellationToken);

    private async Task<string> AssembleCoreAsync(IReadOnlyList<string> keys, int pauseMs, bool chapter, CancellationToken cancellationToken)
    {
        if (keys.Count == 0) throw new ArgumentException("Cannot assemble empty audio.", nameof(keys));
        ArgumentOutOfRangeException.ThrowIfNegative(pauseMs);
        var key = chapter ? AudioCacheKey.Create("chapter-v1", new { sections = keys, pauseMs, profile = "pcm-24000-mono-16" })
            : AudioCacheKey.Section(keys, pauseMs);
        using var lease = await artifacts.AcquireAsync(key, cancellationToken);
        await using (var cached = await artifacts.OpenReadAsync(key, cancellationToken))
        {
            if (cached is not null) return key;
        }
        var directory = Path.Combine(storage.Value.DataFolder, "audio", "v1", "work");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var writer = new WaveFileWriter(path, new WaveFormat(24000, 16, 1)))
            {
                var buffer = new byte[16384];
                for (var index = 0; index < keys.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await using var input = await artifacts.OpenReadAsync(keys[index], cancellationToken)
                        ?? throw new InvalidDataException("A section audio checkpoint is missing or corrupt.");
                    using var reader = new WaveFileReader(input);
                    var normalized = reader.WaveFormat.Encoding == WaveFormatEncoding.Pcm && reader.WaveFormat.SampleRate == 24000 &&
                        reader.WaveFormat.Channels == 1 && reader.WaveFormat.BitsPerSample == 16;
                    if (chapter && !normalized)
                        throw new InvalidDataException("Chapter sections must use 24 kHz mono 16-bit PCM.");
                    if (chapter) AudiobookStorage.EnsureWaveSize(writer.Length, reader.Length);
                    AudiobookStorage.EnsureSpace(directory, reader.Length * 2 + (long)pauseMs * 96);
                    var pcm = normalized ? null : Normalize(reader);
                    int read;
                    while ((read = pcm is null ? await reader.ReadAsync(buffer.AsMemory(), cancellationToken) : pcm.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        AudiobookStorage.EnsureWaveSize(writer.Length, read);
                        await writer.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    }
                    if (index == keys.Count - 1) continue;
                    Array.Clear(buffer);
                    var remaining = (long)pauseMs * 48;
                    AudiobookStorage.EnsureWaveSize(writer.Length, remaining);
                    while (remaining > 0)
                    {
                        var count = (int)Math.Min(remaining, buffer.Length);
                        await writer.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                        remaining -= count;
                    }
                }
            }
            await using var completed = File.OpenRead(path);
            AudiobookStorage.EnsureSpace(directory, completed.Length);
            await artifacts.SaveAsync(key, completed, cancellationToken);
            await artifacts.SaveManifestAsync(new(1, key, null, keys.ToArray()), cancellationToken);
            return key;
        }
        finally { File.Delete(path); }
    }

    private static IWaveProvider Normalize(WaveFileReader reader)
    {
        ISampleProvider samples = reader.ToSampleProvider();
        if (samples.WaveFormat.Channels == 2)
            samples = new StereoToMonoSampleProvider(samples) { LeftVolume = 0.5f, RightVolume = 0.5f };
        else if (samples.WaveFormat.Channels != 1)
            throw new NotSupportedException("Audiobook rendering supports mono or stereo provider audio.");
        if (samples.WaveFormat.SampleRate != 24000)
            samples = new BandLimitedResamplingSampleProvider(samples, 24000);
        return new SampleToWaveProvider16(samples);
    }
}
