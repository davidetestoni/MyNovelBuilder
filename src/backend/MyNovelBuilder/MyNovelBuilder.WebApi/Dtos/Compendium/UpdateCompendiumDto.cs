using FluentValidation;

namespace MyNovelBuilder.WebApi.Dtos.Compendium;

/// <summary>
/// Data transfer object for updating a compendium.
/// </summary>
public class UpdateCompendiumDto
{
    /// <summary>
    /// The compendium's ID.
    /// </summary>
    public required Guid Id { get; set; }
    
    /// <summary>
    /// The compendium's name.
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// The compendium's description.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}

internal class UpdateCompendiumDtoValidator : AbstractValidator<UpdateCompendiumDto>
{
    public UpdateCompendiumDtoValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}
