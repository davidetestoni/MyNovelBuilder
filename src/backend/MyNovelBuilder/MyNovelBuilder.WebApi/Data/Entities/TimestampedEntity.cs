using System.ComponentModel.DataAnnotations;

namespace MyNovelBuilder.WebApi.Data.Entities;

/// <summary>
/// An entity with timestamps for creation and last update.
/// </summary>
public abstract class TimestampedEntity : Entity
{
    /// <summary>
    /// The entity's creation date.
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// The entity's last update date. If the entity has just been created,
    /// this is the same as <see cref="CreatedAt"/>.
    /// </summary>
    [Required]
    public DateTime UpdatedAt { get; set; }
}
