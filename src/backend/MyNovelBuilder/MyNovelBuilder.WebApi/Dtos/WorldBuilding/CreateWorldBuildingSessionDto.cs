using FluentValidation;

namespace MyNovelBuilder.WebApi.Dtos.WorldBuilding;

/// <summary>
/// DTO for creating a world-building session.
/// </summary>
public class CreateWorldBuildingSessionDto
{
    /// <summary>
    /// Optional session name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Optional novel used as context.
    /// </summary>
    public Guid? NovelId { get; set; }

    /// <summary>
    /// Optional chapter used as context.
    /// </summary>
    public int? ChapterIndex { get; set; }

    /// <summary>
    /// Included compendia.
    /// </summary>
    public IEnumerable<Guid> CompendiumIds { get; set; } = [];

    /// <summary>
    /// Included records.
    /// </summary>
    public IEnumerable<Guid> CompendiumRecordIds { get; set; } = [];

    /// <summary>
    /// Freeform world premise.
    /// </summary>
    public string? FreeformPremise { get; set; }
}

internal class CreateWorldBuildingSessionDtoValidator : AbstractValidator<CreateWorldBuildingSessionDto>
{
    public CreateWorldBuildingSessionDtoValidator()
    {
        RuleFor(x => x.Name).MaximumLength(100);
        RuleFor(x => x.NovelId).NotEmpty().When(x => x.NovelId.HasValue);
        RuleFor(x => x.ChapterIndex).GreaterThanOrEqualTo(0).When(x => x.ChapterIndex.HasValue);
        RuleForEach(x => x.CompendiumIds).NotEmpty();
        RuleForEach(x => x.CompendiumRecordIds).NotEmpty();
        RuleFor(x => x.FreeformPremise).MaximumLength(50_000);
    }
}
