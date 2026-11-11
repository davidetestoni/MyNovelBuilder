using System.Buffers.Binary;
using System.Text;

namespace MyNovelBuilder.WebApi.Helpers;

/// <summary>Helper to write prompt metadata into PNG images without re-encoding pixels.</summary>
public static class PngPromptMetadataHelper
{
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly uint[] CrcTable = CreateCrcTable();

    /// <summary>
    /// Adds prompt metadata to a PNG image, returning original bytes if metadata cannot be written.
    /// </summary>
    public static byte[] AddPromptMetadataIfPng(byte[] imageBytes, string prompt, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(prompt) || !imageBytes.AsSpan().StartsWith(PngSignature))
        {
            return imageBytes;
        }

        try
        {
            var endOffset = FindEndChunk(imageBytes);
            // Uncompressed iTXt: keyword, compression flag/method, language,
            // translated keyword, then UTF-8 text (PNG specification section 11.3.4.5).
            var text = Encoding.UTF8.GetBytes("prompt\0\0\0en\0Prompt\0" + prompt);
            using var output = new MemoryStream();
            output.Write(imageBytes.AsSpan(0, endOffset));
            Span<byte> number = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(number, text.Length);
            output.Write(number);
            output.Write("iTXt"u8);
            output.Write(text);
            var crc = UpdateCrc(uint.MaxValue, "iTXt"u8);
            crc = UpdateCrc(crc, text) ^ uint.MaxValue;
            BinaryPrimitives.WriteUInt32BigEndian(number, crc);
            output.Write(number);
            output.Write(imageBytes.AsSpan(endOffset));
            return output.ToArray();
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to inject prompt metadata into PNG. Returning original image bytes.");
            return imageBytes;
        }
    }

    private static int FindEndChunk(ReadOnlySpan<byte> bytes)
    {
        var offset = PngSignature.Length;
        var hasImageData = false;
        while (bytes.Length - offset >= 12)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset, 4));
            if (length > bytes.Length - offset - 12)
            {
                break;
            }

            var type = bytes.Slice(offset + 4, 4);
            if (offset == PngSignature.Length && (!type.SequenceEqual("IHDR"u8) || length != 13))
            {
                break;
            }

            var crcData = bytes.Slice(offset + 4, (int)length + 4);
            var expectedCrc = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset + 8 + (int)length, 4));
            if ((UpdateCrc(uint.MaxValue, crcData) ^ uint.MaxValue) != expectedCrc)
            {
                break;
            }

            if (type.SequenceEqual("IEND"u8))
            {
                if (length == 0 && hasImageData && offset + 12 == bytes.Length)
                {
                    return offset;
                }
                break;
            }

            hasImageData |= type.SequenceEqual("IDAT"u8);
            offset += (int)length + 12;
        }

        throw new InvalidDataException("PNG has invalid or incomplete chunks.");
    }

    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            crc = CrcTable[(crc ^ value) & 255] ^ (crc >> 8);
        }
        return crc;
    }

    private static uint[] CreateCrcTable()
    {
        var table = new uint[256];
        for (uint index = 0; index < table.Length; index++)
        {
            var crc = index;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? 0xedb88320U ^ (crc >> 1) : crc >> 1;
            }
            table[index] = crc;
        }
        return table;
    }
}
