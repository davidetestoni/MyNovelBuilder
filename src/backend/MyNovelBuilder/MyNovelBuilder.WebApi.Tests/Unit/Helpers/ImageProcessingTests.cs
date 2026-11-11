using System.Buffers.Binary;
using System.Text;
using MyNovelBuilder.WebApi.Helpers;
using SkiaSharp;

namespace MyNovelBuilder.WebApi.Tests.Unit.Helpers;

public sealed class ImageProcessingTests
{
    private static byte[] ReadFixture(string name) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Images", name));

    [Theory]
    [InlineData("red.jpg")]
    [InlineData("transparent.png")]
    public void ConvertToPng_PreservesDimensionsAndAlpha(string name)
    {
        var converted = ImageConversionHelper.ConvertToPng(ReadFixture(name));

        Assert.True(converted.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }));
        using var decoded = SKBitmap.Decode(converted);
        Assert.NotNull(decoded);
        Assert.Equal(2, decoded.Width);
        Assert.Equal(3, decoded.Height);
        if (name == "transparent.png")
        {
            Assert.Equal(127, decoded.GetPixel(0, 0).Alpha);
        }
        else
        {
            Assert.True(decoded.GetPixel(0, 0).Red > 230);
        }
    }

    [Fact]
    public void ConvertToPng_RejectsInvalidInput()
    {
        Assert.Throws<InvalidDataException>(() => ImageConversionHelper.ConvertToPng([1, 2, 3]));
    }

    [Fact]
    public void AddPromptMetadata_PreservesOriginalChunksAndWritesUnicodeTextWithValidCrc()
    {
        var original = ReadFixture("transparent.png");
        var result = PngPromptMetadataHelper.AddPromptMetadataIfPng(original, "Caffè 🌧");
        var insertedLength = result.Length - original.Length;
        var offset = original.Length - 12;

        Assert.Equal(original[..offset], result[..offset]);
        Assert.Equal(original[offset..], result[(offset + insertedLength)..]);
        Assert.Equal("iTXt", Encoding.ASCII.GetString(result, offset + 4, 4));
        var length = BinaryPrimitives.ReadInt32BigEndian(result.AsSpan(offset, 4));
        Assert.Equal("prompt\0\0\0en\0Prompt\0Caffè 🌧", Encoding.UTF8.GetString(result, offset + 8, length));
        // Independently computed CRC-32 of the iTXt type and payload.
        Assert.Equal(ExpectedPromptCrc, BinaryPrimitives.ReadUInt32BigEndian(result.AsSpan(offset + 8 + length, 4)));
        using var decoded = SKBitmap.Decode(result);
        Assert.NotNull(decoded);
        Assert.Equal(127, decoded.GetPixel(0, 0).Alpha);
    }

    private const uint ExpectedPromptCrc = 0x280f5fa9;

    [Fact]
    public void AddPromptMetadata_PreservesExistingMetadata()
    {
        var original = PngPromptMetadataHelper.AddPromptMetadataIfPng(ReadFixture("transparent.png"), "first");
        var updated = PngPromptMetadataHelper.AddPromptMetadataIfPng(original, "second");

        Assert.True(updated.Length > original.Length);
        Assert.Equal(original[..^12], updated[..(original.Length - 12)]);
    }

    [Fact]
    public void AddPromptMetadata_ReturnsOriginalForNonPngEmptyPromptAndMalformedChunks()
    {
        var png = ReadFixture("transparent.png");
        var jpeg = ReadFixture("red.jpg");
        Assert.Same(png, PngPromptMetadataHelper.AddPromptMetadataIfPng(png, " "));
        Assert.Same(jpeg, PngPromptMetadataHelper.AddPromptMetadataIfPng(jpeg, "prompt"));
        foreach (var invalid in new[] { png[..8], png[..^1], png.ToArray() })
        {
            if (invalid.Length == png.Length)
            {
                // Use an oversized chunk length.
                invalid[8] = 255;
            }
            Assert.Same(invalid, PngPromptMetadataHelper.AddPromptMetadataIfPng(invalid, "prompt"));
        }
        png[20] ^= 1;
        Assert.Same(png, PngPromptMetadataHelper.AddPromptMetadataIfPng(png, "prompt"));
    }
}
