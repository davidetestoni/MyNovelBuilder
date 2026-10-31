using System.Buffers.Binary;
using MyNovelBuilder.WebApi.Models.AudioGeneration;
using MyNovelBuilder.WebApi.Options;
using MyNovelBuilder.WebApi.Services;
using NAudio.Wave;

namespace MyNovelBuilder.WebApi.Tests.Unit.Services;

public sealed class AudioArtifactAssemblerTests
{
    [Theory]
    [InlineData(24000, 16, false)]
    [InlineData(22051, 8, false)]
    [InlineData(44100, 32, true)]
    public async Task EmbeddedCuesUseFrameOffsetsAndOriginalLabelsWithoutChangingSamples(int rate, int bits, bool floating)
    {
        using var f = new Fixture();
        var format = floating ? WaveFormat.CreateIeeeFloatWaveFormat(rate, 2) : new WaveFormat(rate, bits, 1);
        var pcm = new byte[4 * format.BlockAlign];
        var source = await f.Save(pcm, format);
        var key = await f.Assembler.AssembleChapterAsync([source, source], [0, 11]);
        await using var input = await f.Artifacts.OpenReadAsync(key);
        Assert.NotNull(input);
        using var reader = new BinaryReader(input, System.Text.Encoding.ASCII, leaveOpen: true);
        input.Position = 4;
        Assert.Equal(input.Length - 8, reader.ReadUInt32());
        input.Position = 12;
        var foundCues = false;
        var foundLabels = false;
        while (input.Position < input.Length)
        {
            var id = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4));
            var size = reader.ReadUInt32();
            var end = input.Position + size;
            if (id == "cue ")
            {
                foundCues = true;
                Assert.Equal(2u, reader.ReadUInt32());
                foreach (var (cueId, frame) in new[] { (1u, 0u), (2u, (uint)(4 + rate / 2)) })
                {
                    Assert.Equal(cueId, reader.ReadUInt32());
                    Assert.Equal(frame, reader.ReadUInt32());
                    Assert.Equal("data", System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4)));
                    Assert.Equal(0u, reader.ReadUInt32());
                    Assert.Equal(0u, reader.ReadUInt32());
                    Assert.Equal(frame, reader.ReadUInt32());
                }
            }
            if (id == "LIST")
            {
                foundLabels = true;
                Assert.Equal("adtl", System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4)));
                foreach (var (cueId, label) in new[] { (1u, "Section 1\0"), (2u, "Section 12\0") })
                {
                    Assert.Equal("labl", System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4)));
                    Assert.Equal((uint)(4 + label.Length), reader.ReadUInt32());
                    Assert.Equal(cueId, reader.ReadUInt32());
                    Assert.Equal(label, System.Text.Encoding.ASCII.GetString(reader.ReadBytes(label.Length)));
                    if (label.Length % 2 != 0) Assert.Equal(0, reader.ReadByte());
                }
            }
            input.Position = end + size % 2;
        }
        Assert.True(foundCues && foundLabels);
        var (_, samples) = await f.Read(key);
        Assert.Equal((8 + rate / 2) * format.BlockAlign, samples.Length);
        Assert.Equal(pcm, samples[..pcm.Length]);
        Assert.Equal(pcm, samples[^pcm.Length..]);
        Assert.NotEqual(key, await f.Assembler.AssembleChapterAsync([source, source], [0, 1]));
    }

    [Theory]
    [InlineData(24000, 1, 16, false)]
    [InlineData(44100, 1, 16, false)]
    [InlineData(48000, 2, 16, false)]
    [InlineData(44100, 2, 24, false)]
    [InlineData(48000, 1, 32, false)]
    [InlineData(22050, 1, 8, false)]
    [InlineData(44100, 2, 32, true)]
    public async Task MatchingProviderFormatsArePreservedByteExactlyAcrossSectionAndChapterBuffers(int rate, int channels, int bits, bool floating)
    {
        using var f = new Fixture();
        var format = floating ? WaveFormat.CreateIeeeFloatWaveFormat(rate, channels) : new WaveFormat(rate, bits, channels);
        var pcm = new byte[50002 * format.BlockAlign];
        new Random(42).NextBytes(pcm);
        if (floating)
            for (var i = 0; i < pcm.Length; i += 4) BinaryPrimitives.WriteSingleLittleEndian(pcm.AsSpan(i), (float)Math.Sin(i * 0.001));
        var source = await f.Save(pcm, format);
        var section = await f.Assembler.AssembleAsync([source], 0);
        var chapter = await f.Assembler.AssembleChapterAsync([section]);
        var (actual, output) = await f.Read(chapter);
        Assert.Equal(format, actual);
        Assert.Equal(pcm, output);
    }

    [Fact]
    public async Task MixedRatesUpsampleOnlyLowerRateAndPreserveNativeStereoSegmentAndExactGaps()
    {
        using var f = new Fixture();
        var low = new byte[2400 * 2];
        for (var i = 0; i < 2400; i++)
            BinaryPrimitives.WriteInt16LittleEndian(low.AsSpan(i * 2), (short)(8000 * Math.Sin(2 * Math.PI * 440 * i / 24000)));
        var native = new byte[4800 * 4];
        new Random(42).NextBytes(native);
        var a = await f.Save(low, new WaveFormat(24000, 16, 1));
        var b = await f.Save(native, new WaveFormat(48000, 16, 2));
        var key = await f.Assembler.AssembleAsync([a, b], 150);
        var (format, output) = await f.Read(key);
        Assert.Equal(new WaveFormat(48000, 16, 2), format);
        var manifest = (await f.Artifacts.ReadManifestAsync(key))!;
        Assert.Equal(new long[] { 4800, 4800 }, manifest.SegmentFrames);
        var firstBytes = (int)manifest.SegmentFrames![0] * 4;
        var gapBytes = 7200 * 4;
        Assert.Equal(firstBytes + gapBytes + native.Length, output.Length);
        Assert.All(output.Skip(firstBytes).Take(gapBytes), b => Assert.Equal((byte)0, b));
        Assert.Equal(native, output[(firstBytes + gapBytes)..]);
    }

    [Fact]
    public async Task UnsignedEightBitSilenceAndOddRateGapsUseWholeFrames()
    {
        using var f = new Fixture();
        var source = await f.Save([1, 2, 3, 4], new WaveFormat(22051, 8, 1));
        var key = await f.Assembler.AssembleChapterAsync([source, source]);
        var (_, output) = await f.Read(key);
        Assert.Equal(4 + 11025 + 4, output.Length);
        Assert.All(output.Skip(4).Take(11025), b => Assert.Equal((byte)128, b));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, output[^4..]);
    }

    [Fact]
    public async Task LegacyDownsampledAssemblyIsNotServedForNativeRequest()
    {
        using var f = new Fixture();
        var source = await f.Save(new byte[48000], new WaveFormat(48000, 16, 1));
        var legacy = AudioCacheKey.Create("section-v2", new { chunks = new[] { source }, pauseMs = 0, profile = "pcm-24000-mono-16" });
        await f.Save(new byte[24000], new WaveFormat(24000, 16, 1), legacy);
        var key = await f.Assembler.AssembleAsync([source], 0);
        Assert.NotEqual(legacy, key);
        Assert.Equal(48000, (await f.Read(key)).Format.SampleRate);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "assembly-fidelity-" + Guid.NewGuid());
        public FileSystemAudioArtifactRepository Artifacts { get; }
        public AudioArtifactAssembler Assembler { get; }
        public Fixture()
        {
            var options = Microsoft.Extensions.Options.Options.Create(new AppStorageOptions { DataFolder = folder });
            Artifacts = new(options);
            Assembler = new(Artifacts, options);
        }
        public async Task<string> Save(byte[] pcm, WaveFormat format, string? key = null)
        {
            using var wav = new MemoryStream();
            using (var writer = new WaveFileWriter(wav, format)) await writer.WriteAsync(pcm.AsMemory());
            key ??= AudioCacheKey.Hash(Guid.NewGuid().ToString());
            using var source = new MemoryStream(wav.ToArray());
            await Artifacts.SaveAsync(key, source);
            return key;
        }
        public async Task<(WaveFormat Format, byte[] Pcm)> Read(string key)
        {
            await using var result = await Artifacts.OpenReadAsync(key);
            using var reader = new WaveFileReader(result!);
            using var output = new MemoryStream();
            var buffer = new byte[16384 - 16384 % reader.WaveFormat.BlockAlign];
            int read;
            while ((read = await reader.ReadAsync(buffer.AsMemory())) > 0) await output.WriteAsync(buffer.AsMemory(0, read));
            return (reader.WaveFormat, output.ToArray());
        }
        public void Dispose() { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }
}
