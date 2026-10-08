using Microsoft.Extensions.Options;
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
    {
        if (keys.Count == 0) throw new ArgumentException("Cannot assemble empty audio.", nameof(keys));
        ArgumentOutOfRangeException.ThrowIfNegative(pauseMs);
        var key = AudioCacheKey.Section(keys, pauseMs);
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
                    ISampleProvider samples = reader.ToSampleProvider();
                    if (samples.WaveFormat.Channels == 2)
                        samples = new StereoToMonoSampleProvider(samples) { LeftVolume = 0.5f, RightVolume = 0.5f };
                    else if (samples.WaveFormat.Channels != 1)
                        throw new NotSupportedException("Audiobook rendering supports mono or stereo provider audio.");
                    if (samples.WaveFormat.SampleRate != 24000) samples = new WdlResamplingSampleProvider(samples, 24000);
                    var pcm = new SampleToWaveProvider16(samples);
                    int read;
                    while ((read = pcm.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        CheckSize(writer.Length, read);
                        await writer.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    }
                    if (index == keys.Count - 1) continue;
                    Array.Clear(buffer);
                    var remaining = (long)pauseMs * 48;
                    CheckSize(writer.Length, remaining);
                    while (remaining > 0)
                    {
                        var count = (int)Math.Min(remaining, buffer.Length);
                        await writer.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                        remaining -= count;
                    }
                }
            }
            await using var completed = File.OpenRead(path);
            await artifacts.SaveAsync(key, completed, cancellationToken);
            return key;
        }
        finally { File.Delete(path); }
    }

    private static void CheckSize(long length, long additional)
    {
        if (length + additional > uint.MaxValue - 44L)
            throw new InvalidDataException("This section exceeds the WAV container limit. Split it into smaller sections.");
    }
}
