namespace MyNovelBuilder.WebApi.Dtos.MediaLibrary;

/// <summary>
/// Data transfer object for a media file inside a linked folder.
/// </summary>
public class MediaFileDto
{
    /// <summary>
    /// The file name, including extension.
    /// </summary>
    public required string FileName { get; set; }

    /// <summary>
    /// The file's last modified date and time in UTC.
    /// </summary>
    public required DateTime LastModifiedAt { get; set; }

    /// <summary>
    /// The file size in bytes.
    /// </summary>
    public required long SizeBytes { get; set; }
}
