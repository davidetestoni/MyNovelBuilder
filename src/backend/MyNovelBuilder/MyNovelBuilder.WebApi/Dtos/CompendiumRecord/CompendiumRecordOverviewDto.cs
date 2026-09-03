using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Dtos.CompendiumRecord;

/// <summary>
/// Data transfer object for an overview of a compendium record.
/// </summary>
public class CompendiumRecordOverviewDto
{
    /// <summary>
    /// The compendium record's ID.
    /// </summary>
    public Guid Id { get; set; }
    
    /// <summary>
    /// The compendium record's name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The compendium record's type.
    /// </summary>
    public CompendiumRecordType Type { get; set; }

    /// <summary>
    /// The compendium record's image URL.
    /// </summary>
    public string? ImageUrl { get; set; }
}
