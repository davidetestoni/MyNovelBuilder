namespace MyNovelBuilder.WebApi.Models.MediaLibrary;

/// <summary>
/// The content of a media file.
/// </summary>
public class MediaFileContent
{
    /// <summary>
    /// The file name, including extension.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// The response content type.
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// The raw file bytes.
    /// </summary>
    public required byte[] Bytes { get; init; }
}
