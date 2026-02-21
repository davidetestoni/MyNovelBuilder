using FluentValidation;

namespace MyNovelBuilder.WebApi.Models.Novels;

internal class ProseValidator : AbstractValidator<Prose>
{
    public ProseValidator()
    {
        RuleFor(x => x.Chapters).NotNull();
        RuleForEach(x => x.Chapters).SetValidator(new ChapterValidator());
    }
}

internal class ChapterValidator : AbstractValidator<Chapter>
{
    public ChapterValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Sections).NotNull();
        RuleForEach(x => x.Sections).SetValidator(new SectionValidator());
        RuleFor(x => x.StoryEvents).NotNull();
        RuleForEach(x => x.StoryEvents).SetValidator(new StoryEventValidator());
    }
}

internal class SectionValidator : AbstractValidator<Section>
{
    public SectionValidator()
    {
        RuleFor(x => x.Summary).MaximumLength(10_000);
        RuleFor(x => x.Text).MaximumLength(200_000);
        RuleFor(x => x.Images).NotNull();
        RuleForEach(x => x.Images).NotEmpty().MaximumLength(2_000);
        RuleFor(x => x.RecordOverrides).NotNull();
        RuleForEach(x => x.RecordOverrides).SetValidator(new RecordOverrideValidator());
    }
}

internal class StoryEventValidator : AbstractValidator<StoryEvent>
{
    public StoryEventValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Date).MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(5_000);
    }
}

internal class RecordOverrideValidator : AbstractValidator<RecordOverride>
{
    public RecordOverrideValidator()
    {
        RuleFor(x => x.CompendiumRecordId).NotEmpty();
        RuleFor(x => x.Keyword).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(20_000);
    }
}
