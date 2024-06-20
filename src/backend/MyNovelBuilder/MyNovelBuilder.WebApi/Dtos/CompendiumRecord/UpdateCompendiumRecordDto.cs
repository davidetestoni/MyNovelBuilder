using FluentValidation;
using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Dtos.CompendiumRecord;

/// <summary>
/// Data transfer object for updating a compendium record.
/// </summary>
public class UpdateCompendiumRecordDto
{
    /// <summary>
    /// The record's ID.
    /// </summary>
    public required Guid Id { get; set; }
    
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
    /// The record's context.
    /// </summary>
    public string Context { get; set; } = string.Empty;
}

internal class UpdateCompendiumRecordDtoValidator : AbstractValidator<UpdateCompendiumRecordDto>
{
    public UpdateCompendiumRecordDtoValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Aliases).MaximumLength(500);
        RuleFor(x => x.Type).IsInEnum();
    }
}
