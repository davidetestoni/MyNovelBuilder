using FluentValidation;
using MyNovelBuilder.WebApi.Models.Chats;

namespace MyNovelBuilder.WebApi.Dtos.WorldBuilding;

/// <summary>
/// DTO for updating a world-building session.
/// </summary>
public class UpdateWorldBuildingSessionDto
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

    /// <summary>
    /// Session messages.
    /// </summary>
    public IEnumerable<ChatMessage> Messages { get; set; } = [];
}

internal class UpdateWorldBuildingSessionDtoValidator : AbstractValidator<UpdateWorldBuildingSessionDto>
{
    public UpdateWorldBuildingSessionDtoValidator()
    {
        RuleFor(x => x.Name).MaximumLength(100);
        RuleFor(x => x.NovelId).NotEmpty().When(x => x.NovelId.HasValue);
        RuleFor(x => x.ChapterIndex).GreaterThanOrEqualTo(0).When(x => x.ChapterIndex.HasValue);
        RuleForEach(x => x.CompendiumIds).NotEmpty();
        RuleForEach(x => x.CompendiumRecordIds).NotEmpty();
        RuleFor(x => x.FreeformPremise).MaximumLength(50_000);
        RuleForEach(x => x.Messages).ChildRules(message =>
        {
            message.RuleFor(x => x.Id).NotEmpty();
            message.RuleFor(x => x.Role).IsInEnum();
            message.RuleFor(x => x.TextContent).NotEmpty().MaximumLength(50_000);
            message.RuleFor(x => x.StructuredContent).MaximumLength(200_000);
        });
    }
}
