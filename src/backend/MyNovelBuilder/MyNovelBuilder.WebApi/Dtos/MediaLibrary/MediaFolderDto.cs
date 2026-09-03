namespace MyNovelBuilder.WebApi.Dtos.MediaLibrary;

/// <summary>
/// Data transfer object for a linked media folder.
/// </summary>
public class MediaFolderDto
{
    /// <summary>
    /// The folder's ID.
    /// </summary>
    public required Guid Id { get; set; }

    /// <summary>
    /// The date and time the link was created.
    /// </summary>
    public required DateTime CreatedAt { get; set; }

    /// <summary>
    /// The date and time the link was last updated.
    /// </summary>
    public required DateTime UpdatedAt { get; set; }

    /// <summary>
    /// The display name of the folder.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The local filesystem path of the folder.
    /// </summary>
    public required string Path { get; set; }
}
