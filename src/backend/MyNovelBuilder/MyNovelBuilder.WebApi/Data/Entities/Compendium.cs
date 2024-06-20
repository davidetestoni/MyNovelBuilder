using System.ComponentModel.DataAnnotations;

namespace MyNovelBuilder.WebApi.Data.Entities;

/// <summary>
/// A compendium.
/// </summary>
public class Compendium : TimestampedEntity
{
    /// <summary>
    /// The compendium's name.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public required string Name { get; init; }

    /// <summary>
    /// The compendium's description.
    /// </summary>
    [MaxLength(500)]
    public string Description { get; init; } = string.Empty;
    
    /// <summary>
    /// The records in the compendium.
    /// </summary>
    public IEnumerable<CompendiumRecord> Records { get; init; } = new List<CompendiumRecord>();
    
    /// <summary>
    /// The novels that use the compendium.
    /// </summary>
    public IEnumerable<Novel> Novels { get; init; } = new List<Novel>();
}
