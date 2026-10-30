using System.Buffers.Binary;
using MyNovelBuilder.WebApi.Helpers;
using NAudio.Wave;

namespace MyNovelBuilder.WebApi.Tests.Unit.Helpers;

public sealed class WavGainStreamTests
{
    [Theory]
    [InlineData(6.020599913279624, 2000)]
    [InlineData(-6.020599913279624, 500)]
    [InlineData(0, 1000)]
    public async Task GainScalesSamplesAndPreservesHeader(double gain, short expected)
    {
        var wav = CreateWav();
        var result = await WavGainStream.ApplyAsync(wav, gain, default);
        Assert.Equal(wav.Length, result.Length);
        using var reader = new WaveFileReader(new MemoryStream(result));
        var samples = new byte[4];
        Assert.Equal(4, reader.Read(samples, 0, 4));
        Assert.Equal(expected, BinaryPrimitives.ReadInt16LittleEndian(samples));
        Assert.Equal(-expected, BinaryPrimitives.ReadInt16LittleEndian(samples.AsSpan(2)));
        Assert.Equal(CreateWav(), wav);
    }

    [Fact]
    public async Task FragmentedStreamingMatchesBufferedGain()
    {
        var wav = CreateWav();
        var expected = await WavGainStream.ApplyAsync(wav, -6, default);
        await using var stream = new WavGainStream(new FragmentedStream(wav), -6);
        using var output = new MemoryStream();
        var singleByte = new byte[1];
        while (await stream.ReadAsync(singleByte.AsMemory()) > 0) output.Write(singleByte);
        Assert.Equal(expected, output.ToArray());
    }

    private static byte[] CreateWav()
    {
        using var output = new MemoryStream();
        using (var writer = new WaveFileWriter(output, new WaveFormat(24000, 16, 1)))
        {
            writer.Write(new byte[] { 0xe8, 0x03, 0x18, 0xfc }, 0, 4);
            writer.Flush();
            return output.ToArray();
        }
    }

    private sealed class FragmentedStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) =>
            base.ReadAsync(buffer[..Math.Min(buffer.Length, 1)], ct);
    }
}
