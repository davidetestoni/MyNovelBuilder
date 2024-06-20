using System.ComponentModel.DataAnnotations;
using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Data.Entities;

/// <summary>
/// Entity for a compendium record.
/// </summary>
public class CompendiumRecord : TimestampedEntity
{
    /// <summary>
    /// The record's name.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public required string Name { get; init; }
    
    /// <summary>
    /// The record's aliases, i.e. other names by which it is known.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Aliases { get; init; } = string.Empty;
    
    /// <summary>
    /// The record type.
    /// </summary>
    [Required]
    public CompendiumRecordType Type { get; init; }
    
    /// <summary>
    /// The record's context.
    /// </summary>
    [Required]
    [MaxLength(10000)]
    public string Context { get; init; } = string.Empty;
    
    /// <summary>
    /// The current image ID.
    /// </summary>
    public Guid? CurrentImageId { get; set; }
    
    /// <summary>
    /// The compendium to which the record belongs.
    /// </summary>
    public Compendium Compendium { get; init; } = null!;
}
