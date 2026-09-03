using System.ComponentModel.DataAnnotations;

namespace MyNovelBuilder.WebApi.Data.Entities;

/// <summary>
/// A linked media folder on the local filesystem.
/// </summary>
public class MediaFolder : TimestampedEntity
{
    /// <summary>
    /// The display name of the linked folder.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public required string Name { get; set; }

    /// <summary>
    /// The linked folder path on disk.
    /// </summary>
    [Required]
    [MaxLength(1000)]
    public required string Path { get; set; }
}
