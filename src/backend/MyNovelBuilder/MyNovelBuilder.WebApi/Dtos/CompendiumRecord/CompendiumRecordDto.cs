using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Dtos.CompendiumRecord;

public class CompendiumRecordDto
{
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
    /// The current image ID.
    /// </summary>
    public Guid? CurrentImageId { get; set; }
    
    /// <summary>
    /// The ID of the compendium to which the record belongs.
    /// </summary>
    public required Guid CompendiumId { get; set; }
}
