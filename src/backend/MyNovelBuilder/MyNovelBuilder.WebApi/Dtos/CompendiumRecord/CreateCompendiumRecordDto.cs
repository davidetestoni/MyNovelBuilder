using FluentValidation;
using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Dtos.CompendiumRecord;

/// <summary>
/// Data transfer object for creating a compendium record.
/// </summary>
public class CreateCompendiumRecordDto
{
    /// <summary>
    /// The record's name.
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// The record's aliases, i.e. other names by which it is known.
    /// </summary>
    public string Aliases { get; set; } = string.Empty;
    
    /// <summary>
    /// The record type.
    /// </summary>
    public CompendiumRecordType Type { get; set; }
    
    /// <summary>
    /// The ID of the compendium to which the record belongs.
    /// </summary>
    public Guid CompendiumId { get; set; }
}

internal class CreateCompendiumRecordDtoValidator : AbstractValidator<CreateCompendiumRecordDto>
{
    public CreateCompendiumRecordDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Aliases).MaximumLength(500);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.CompendiumId).NotEmpty();
    }
}
