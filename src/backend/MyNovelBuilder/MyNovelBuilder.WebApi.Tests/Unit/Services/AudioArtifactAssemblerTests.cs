using System.Buffers.Binary;
using MyNovelBuilder.WebApi.Models.AudioGeneration;
using MyNovelBuilder.WebApi.Options;
using MyNovelBuilder.WebApi.Services;
using NAudio.Wave;

namespace MyNovelBuilder.WebApi.Tests.Unit.Services;

public sealed class AudioArtifactAssemblerTests
{
    [Theory]
    [InlineData(44100, 8000, 0.95, 1.01)]
    [InlineData(48000, 8000, 0.95, 1.01)]
    [InlineData(44100, 15000, 0, 0.001)]
    [InlineData(48000, 15000, 0, 0.001)]
    public async Task DownsamplingPreservesSpeechBandAndRejectsAliasedHighFrequencies(
        int rate, int frequency, double minimum, double maximum)
    {
        var pcm = new byte[rate * 2];
        for (var i = 0; i < rate; i++)
            BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(i * 2), (short)(16000 * Math.Sin(2 * Math.PI * frequency * i / rate)));
        var output = await Assemble(pcm, rate);
        Assert.Equal(24000 * sizeof(short), output.Length);
        // Ignore filter startup/end transients; inspect the middle across multiple assembly buffers.
        var samples = Enumerable.Range(2400, 19200)
            .Select(i => (double)BinaryPrimitives.ReadInt16LittleEndian(output.AsSpan(i * 2))).ToArray();
        var relativeRms = Math.Sqrt(samples.Average(s => s * s)) / (16000 / Math.Sqrt(2));
        Assert.InRange(relativeRms, minimum, maximum);
    }

    [Fact]
    public async Task AlreadyNormalizedPcmIsPreservedExactlyAcrossBuffersAndChapterAssembly()
    {
        var pcm = new byte[96000];
        new Random(42).NextBytes(pcm);
        Assert.Equal(pcm, await Assemble(pcm, 24000));
    }

    private static async Task<byte[]> Assemble(byte[] pcm, int rate)
    {
        var folder = Path.Combine(Path.GetTempPath(), "assembly-fidelity-" + Guid.NewGuid());
        try
        {
            var options = Microsoft.Extensions.Options.Options.Create(new AppStorageOptions { DataFolder = folder });
            var artifacts = new FileSystemAudioArtifactRepository(options);
            var assembler = new AudioArtifactAssembler(artifacts, options);
            using var wav = new MemoryStream();
            using (var writer = new WaveFileWriter(wav, new WaveFormat(rate, 16, 1)))
                await writer.WriteAsync(pcm.AsMemory());
            var key = AudioCacheKey.Hash("source");
            using var source = new MemoryStream(wav.ToArray());
            await artifacts.SaveAsync(key, source);
            var section = await assembler.AssembleAsync([key], 0);
            var chapter = await assembler.AssembleChapterAsync([section]);
            await using var result = await artifacts.OpenReadAsync(chapter);
            using var reader = new WaveFileReader(result!);
            using var output = new MemoryStream();
            await reader.CopyToAsync(output);
            return output.ToArray();
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }
}
