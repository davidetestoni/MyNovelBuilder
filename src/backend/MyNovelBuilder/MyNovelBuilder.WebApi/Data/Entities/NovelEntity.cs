using System.ComponentModel.DataAnnotations;

namespace MyNovelBuilder.WebApi.Data.Entities;

/// <summary>
/// A novel.
/// </summary>
public class NovelEntity : TimestampedEntity
{
    /// <summary>
    /// The novel's title.
    /// </summary>
    [Required]
    public required string Title { get; set; }
}
