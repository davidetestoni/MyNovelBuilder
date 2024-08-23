using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Dtos.CompendiumRecord;

/// <summary>
/// Data transfer object for a compendium record.
/// </summary>
public class CompendiumRecordDto
{
    /// <summary>
    /// The record's ID.
    /// </summary>
    public required Guid Id { get; set; }
    
    /// <summary>
    /// The date and time the compendium record was created.
    /// </summary>
    public required DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// The date and time the compendium record was last updated.
    /// </summary>
    public required DateTime UpdatedAt { get; set; }
    
    /// <summary>
    /// The record's name.
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// The record's aliases, i.e. other names by which it is known.
    /// </summary>
    public required string Aliases { get; set; } = string.Empty;
    
    /// <summary>
    /// The record type.
    /// </summary>
    public required CompendiumRecordType Type { get; set; }
    
    /// <summary>
    /// The record's context.
    /// </summary>
    public string Context { get; set; } = string.Empty;

    /// <summary>
    /// The current image ID, if any.
    /// </summary>
    public Guid? CurrentImageId { get; set; }
    
    /// <summary>
    /// The compendium record's images.
    /// </summary>
    public IEnumerable<CompendiumRecordImageDto> Images { get; set; } = Array.Empty<CompendiumRecordImageDto>();
    
    /// <summary>
    /// The ID of the compendium to which the record belongs.
    /// </summary>
    public required Guid CompendiumId { get; set; }
    
    /// <summary>
    /// Whether the record should always be included in the prompts.
    /// </summary>
    public bool AlwaysIncluded { get; set; }
}
