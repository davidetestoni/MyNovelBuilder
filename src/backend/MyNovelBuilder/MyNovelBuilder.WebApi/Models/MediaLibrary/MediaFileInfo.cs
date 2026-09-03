namespace MyNovelBuilder.WebApi.Models.MediaLibrary;

/// <summary>
/// Metadata for a media file inside a linked folder.
/// </summary>
public class MediaFileInfo
{
    /// <summary>
    /// The file name, including extension.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// The file's last modified date and time in UTC.
    /// </summary>
    public required DateTime LastModifiedAt { get; init; }

    /// <summary>
    /// The file size in bytes.
    /// </summary>
    public required long SizeBytes { get; init; }
}
