using System.Buffers.Binary;
using Microsoft.Extensions.Options;
using MyNovelBuilder.WebApi.Helpers;
using MyNovelBuilder.WebApi.Models.AudioGeneration;
using MyNovelBuilder.WebApi.Options;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>Bounded-buffer assembly of finite WAV artifacts, preserving matching source formats.</summary>
public sealed class AudioArtifactAssembler(IAudioArtifactRepository artifacts, IOptions<AppStorageOptions> storage)
{
    /// <summary>Concatenates speech units with pauses only between units, never after the last unit.</summary>
    public async Task<string> AssembleAsync(IReadOnlyList<string> keys, int pauseMs, CancellationToken cancellationToken = default)
        => await AssembleCoreAsync(keys, pauseMs, false, cancellationToken);

    /// <summary>Concatenates section audio into a cached chapter with a 500 ms gap and no trailing silence.</summary>
    public Task<string> AssembleChapterAsync(IReadOnlyList<string> keys, CancellationToken cancellationToken = default)
        => AssembleCoreAsync(keys, 500, true, cancellationToken);

    private async Task<string> AssembleCoreAsync(IReadOnlyList<string> keys, int pauseMs, bool chapter, CancellationToken cancellationToken)
    {
        if (keys.Count == 0) throw new ArgumentException("Cannot assemble empty audio.", nameof(keys));
        ArgumentOutOfRangeException.ThrowIfNegative(pauseMs);
        var key = chapter ? AudioCacheKey.Create("chapter-v2", new { sections = keys, pauseMs, profile = AudioCacheKey.AssemblyProfile })
            : AudioCacheKey.Section(keys, pauseMs);
        using var lease = await artifacts.AcquireAsync(key, cancellationToken);
        await using (var cached = await artifacts.OpenReadAsync(key, cancellationToken))
        {
            if (cached is not null && await artifacts.ReadManifestAsync(key, cancellationToken) is
                { SegmentFrames: { } frames, RenderingProfile: AudioCacheKey.AssemblyProfile } && frames.Length == keys.Count) return key;
        }
        var directory = Path.Combine(storage.Value.DataFolder, "audio", "v1", "work");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            var format = await SelectFormatAsync(keys, cancellationToken);
            var segmentFrames = new List<long>();
            using (var writer = new WaveFileWriter(path, format))
            {
                var buffer = new byte[16384 - 16384 % format.BlockAlign];
                for (var index = 0; index < keys.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await using var input = await artifacts.OpenReadAsync(keys[index], cancellationToken)
                        ?? throw new InvalidDataException("A section audio checkpoint is missing or corrupt.");
                    using var reader = new WaveFileReader(input);
                    var start = writer.Length;
                    var estimatedBytes = (long)Math.Ceiling(reader.Length / (double)reader.WaveFormat.BlockAlign
                        * format.SampleRate / reader.WaveFormat.SampleRate) * format.BlockAlign;
                    var gapBytes = (long)pauseMs * format.SampleRate / 1000 * format.BlockAlign;
                    AudiobookStorage.EnsureSpace(directory, estimatedBytes + gapBytes);
                    var pcm = reader.WaveFormat.Equals(format) ? null : ConvertFormat(reader, format);
                    int read;
                    while ((read = pcm is null ? await reader.ReadAsync(buffer.AsMemory(), cancellationToken) : pcm.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        AudiobookStorage.EnsureWaveSize(writer.Length, read);
                        await writer.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    }
                    segmentFrames.Add((writer.Length - start) / format.BlockAlign);
                    if (index == keys.Count - 1) continue;
                    Array.Fill(buffer, format.Encoding == WaveFormatEncoding.Pcm && format.BitsPerSample == 8 ? (byte)128 : (byte)0);
                    var remaining = gapBytes;
                    AudiobookStorage.EnsureWaveSize(writer.Length, remaining);
                    while (remaining > 0)
                    {
                        var count = (int)Math.Min(remaining, buffer.Length);
                        await writer.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                        remaining -= count;
                    }
                }
            }
            await PadDataChunkAsync(path, cancellationToken);
            await using var completed = File.OpenRead(path);
            AudiobookStorage.EnsureSpace(directory, completed.Length);
            await artifacts.SaveAsync(key, completed, cancellationToken);
            await artifacts.SaveManifestAsync(new(1, key, null, keys.ToArray(), AudioCacheKey.AssemblyProfile, segmentFrames.ToArray()), cancellationToken);
            return key;
        }
        finally { File.Delete(path); }
    }

    private static async Task PadDataChunkAsync(string path, CancellationToken ct)
    {
        if (new FileInfo(path).Length % 2 == 0) return;
        // RIFF chunks require a pad byte for odd-sized 8/24-bit data. It is not an audio frame.
        await using var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 4096, true);
        file.Position = file.Length;
        await file.WriteAsync(new byte[1], ct);
        var size = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(size, checked((uint)(file.Length - 8)));
        file.Position = 4;
        await file.WriteAsync(size, ct);
    }

    private async Task<WaveFormat> SelectFormatAsync(IReadOnlyList<string> keys, CancellationToken ct)
    {
        var formats = new List<WaveFormat>();
        foreach (var key in keys)
        {
            await using var input = await artifacts.OpenReadAsync(key, ct)
                ?? throw new InvalidDataException("A section audio checkpoint is missing or corrupt.");
            using var reader = new WaveFileReader(input);
            if (reader.WaveFormat.Encoding is not (WaveFormatEncoding.Pcm or WaveFormatEncoding.IeeeFloat))
                throw new NotSupportedException("Audiobook assembly requires PCM or IEEE float WAV audio.");
            formats.Add(reader.WaveFormat);
        }
        if (formats.All(f => f.Equals(formats[0]))) return formats[0];
        var channels = formats.Max(f => f.Channels);
        if (channels > 2) throw new NotSupportedException("Mixed audiobook formats support mono or stereo audio.");
        var rate = formats.Max(f => f.SampleRate);
        return formats.All(f => f.Encoding == WaveFormatEncoding.Pcm && f.BitsPerSample == 16)
            ? new WaveFormat(rate, 16, channels) : WaveFormat.CreateIeeeFloatWaveFormat(rate, channels);
    }

    private static IWaveProvider ConvertFormat(WaveFileReader reader, WaveFormat target)
    {
        ISampleProvider samples = reader.ToSampleProvider();
        if (samples.WaveFormat.Channels == 1 && target.Channels == 2)
            samples = new MonoToStereoSampleProvider(samples);
        if (samples.WaveFormat.Channels != target.Channels)
            throw new NotSupportedException("Unsupported audiobook channel conversion.");
        if (samples.WaveFormat.SampleRate != target.SampleRate)
            samples = new BandLimitedResamplingSampleProvider(samples, target.SampleRate);
        return target.Encoding == WaveFormatEncoding.IeeeFloat
            ? new SampleToWaveProvider(samples) : new SampleToWaveProvider16(samples);
    }
}
