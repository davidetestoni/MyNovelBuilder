using FluentValidation;
using MyNovelBuilder.WebApi.Models.WorldBuilding;

namespace MyNovelBuilder.WebApi.Dtos.WorldBuilding;

/// <summary>
/// DTO for editing a pending world-building proposal before acceptance.
/// </summary>
public class UpdateWorldBuildingProposalDto
{
    /// <summary>
    /// Updated operation.
    /// </summary>
    public required WorldBuildingOperation Operation { get; set; }

    /// <summary>
    /// Updated rationale.
    /// </summary>
    public string? Rationale { get; set; }
}

internal class UpdateWorldBuildingProposalDtoValidator : AbstractValidator<UpdateWorldBuildingProposalDto>
{
    public UpdateWorldBuildingProposalDtoValidator()
    {
        RuleFor(x => x.Operation).NotNull().SetValidator(new WorldBuildingOperationValidator());
        RuleFor(x => x.Rationale).MaximumLength(2_000);
    }
}

internal class WorldBuildingOperationValidator : AbstractValidator<WorldBuildingOperation>
{
    public WorldBuildingOperationValidator()
    {
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Aliases).MaximumLength(500);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Context).MaximumLength(10_000);
        RuleFor(x => x.Context).NotEmpty()
            .When(x => x.Kind is WorldBuildingOperationKind.CreateCompendiumRecord
                or WorldBuildingOperationKind.UpdateCompendiumRecord);
    }
}
