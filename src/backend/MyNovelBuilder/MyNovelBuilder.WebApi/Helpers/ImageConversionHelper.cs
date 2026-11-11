using SkiaSharp;

namespace MyNovelBuilder.WebApi.Helpers;

/// <summary>Converts supported raster images to PNG.</summary>
public static class ImageConversionHelper
{
    /// <summary>Decodes an image and returns its first frame as PNG.</summary>
    public static byte[] ConvertToPng(byte[] imageBytes)
    {
        using var data = SKData.CreateCopy(imageBytes);
        using var codec = SKCodec.Create(data)
            ?? throw new InvalidDataException("The uploaded image format is unsupported or invalid.");
        using var image = SKBitmap.Decode(codec)
            ?? throw new InvalidDataException("The uploaded image format is unsupported or invalid.");
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidDataException("The uploaded image could not be encoded as PNG.");
        return encoded.ToArray();
    }
}
