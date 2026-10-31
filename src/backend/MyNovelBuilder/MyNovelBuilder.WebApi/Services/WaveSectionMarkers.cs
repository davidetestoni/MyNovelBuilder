using System.Text;
using System.Globalization;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>Adds standard RIFF cue points and ASCII labels without rewriting audio samples.</summary>
internal static class WaveSectionMarkers
{
    internal static async Task AppendAsync(string path, IReadOnlyList<long> segmentFrames,
        IReadOnlyList<int> sectionIndices, int sampleRate, int pauseMs, CancellationToken ct)
    {
        using var metadata = new MemoryStream();
        using (var writer = new BinaryWriter(metadata, Encoding.ASCII, leaveOpen: true))
        {
            writer.Write("cue "u8);
            writer.Write(checked((uint)(4L + 24L * segmentFrames.Count)));
            writer.Write((uint)segmentFrames.Count);
            long frame = 0;
            for (var i = 0; i < segmentFrames.Count; i++)
            {
                writer.Write((uint)i + 1);
                writer.Write(checked((uint)frame));
                writer.Write("data"u8);
                writer.Write(0u); // Chunk start and block start for a single data chunk.
                writer.Write(0u);
                writer.Write(checked((uint)frame));
                frame += segmentFrames[i] + (long)sampleRate * pauseMs / 1000;
            }
            writer.Write("LIST"u8);
            var listSizePosition = metadata.Position;
            writer.Write(0u);
            writer.Write("adtl"u8);
            for (var i = 0; i < sectionIndices.Count; i++)
            {
                var label = Encoding.ASCII.GetBytes(string.Create(CultureInfo.InvariantCulture, $"Section {(long)sectionIndices[i] + 1}\0"));
                writer.Write("labl"u8);
                writer.Write(checked((uint)(4 + label.Length)));
                writer.Write((uint)i + 1);
                writer.Write(label);
                if (label.Length % 2 != 0) writer.Write((byte)0);
            }
            var end = metadata.Position;
            metadata.Position = listSizePosition;
            writer.Write(checked((uint)(end - listSizePosition - 4)));
            metadata.Position = end;
        }
        await using var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 4096, true);
        AudiobookStorage.EnsureWaveSize(file.Length - 44, metadata.Length);
        AudiobookStorage.EnsureSpace(Path.GetDirectoryName(path)!, metadata.Length);
        file.Position = file.Length;
        metadata.Position = 0;
        await metadata.CopyToAsync(file, ct);
        file.Position = 4;
        var size = BitConverter.GetBytes(checked((uint)(file.Length - 8)));
        if (!BitConverter.IsLittleEndian) Array.Reverse(size);
        await file.WriteAsync(size, ct);
    }
}
