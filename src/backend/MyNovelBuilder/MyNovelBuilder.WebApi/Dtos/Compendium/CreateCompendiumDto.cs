using FluentValidation;

namespace MyNovelBuilder.WebApi.Dtos.Compendium;

/// <summary>
/// Data transfer object for creating a compendium.
/// </summary>
public class CreateCompendiumDto
{
    /// <summary>
    /// The compendium's name.
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// The compendium's description.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}

internal class CreateCompendiumDtoValidator : AbstractValidator<CreateCompendiumDto>
{
    public CreateCompendiumDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}
