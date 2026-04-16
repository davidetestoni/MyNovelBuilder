using FluentValidation;

namespace MyNovelBuilder.WebApi.Dtos.Generate;

internal class TextGenerationContextInfoDtoValidator : AbstractValidator<TextGenerationContextInfoDto>
{
    public TextGenerationContextInfoDtoValidator()
    {
        RuleFor(x => x).SetInheritanceValidator(v =>
        {
            v.Add(new GenerateTextContextInfoDtoValidator());
            v.Add(new SummarizeTextContextInfoDtoValidator());
            v.Add(new ReplaceTextContextInfoDtoValidator());
            v.Add(new CreateCompendiumRecordContextInfoDtoValidator());
            v.Add(new EditCompendiumRecordContextInfoDtoValidator());
            v.Add(new SendChatMessageContextInfoDtoValidator());
            v.Add(new DescribeImageContextInfoDtoValidator());
            v.Add(new DescribeCompendiumImageContextInfoDtoValidator());
            v.Add(new CreateCompendiumRecordImageGenerationPromptContextInfoDtoValidator());
            v.Add(new PrepareImmersiveTtsContextInfoDtoValidator());
            v.Add(new CreateStoryEventsContextInfoDtoValidator());
            v.Add(new TranslateNovelContextInfoDtoValidator());
        });
    }
}

internal class GenerateTextContextInfoDtoValidator : AbstractValidator<GenerateTextContextInfoDto>
{
    public GenerateTextContextInfoDtoValidator()
    {
        RuleFor(x => x.NovelId).NotEmpty();
        RuleFor(x => x.ChapterIndex).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SectionIndex).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TextOffset).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Instructions).MaximumLength(5_000);
    }
}

internal class SummarizeTextContextInfoDtoValidator : AbstractValidator<SummarizeTextContextInfoDto>
{
    public SummarizeTextContextInfoDtoValidator()
    {
        RuleFor(x => x.NovelId).NotEmpty();
        RuleFor(x => x.ChapterIndex).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SectionIndex).GreaterThanOrEqualTo(0);
    }
}

internal class ReplaceTextContextInfoDtoValidator : AbstractValidator<ReplaceTextContextInfoDto>
{
    public ReplaceTextContextInfoDtoValidator()
    {
        RuleFor(x => x.NovelId).NotEmpty();
        RuleFor(x => x.ChapterIndex).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SectionIndex).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TextOffset).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TextLength).GreaterThan(0);
        RuleFor(x => x.Instructions).MaximumLength(5_000);
    }
}

internal class CreateCompendiumRecordContextInfoDtoValidator : AbstractValidator<CreateCompendiumRecordContextInfoDto>
{
    public CreateCompendiumRecordContextInfoDtoValidator()
    {
        RuleFor(x => x.NovelId).NotEmpty();
        RuleFor(x => x.ChapterIndex).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SectionIndex).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TextOffset).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TextLength).GreaterThan(0);
        RuleFor(x => x.Instructions).MaximumLength(5_000);
    }
}

internal class EditCompendiumRecordContextInfoDtoValidator : AbstractValidator<EditCompendiumRecordContextInfoDto>
{
    public EditCompendiumRecordContextInfoDtoValidator()
    {
        RuleFor(x => x.NovelId).NotEmpty();
        RuleFor(x => x.ChapterIndex).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SectionIndex).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TextOffset).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TextLength).GreaterThan(0);
        RuleFor(x => x.RecordId).NotEmpty();
        RuleFor(x => x.Instructions).MaximumLength(5_000);
    }
}

internal class ChatMessageDtoValidator : AbstractValidator<ChatMessageDto>
{
    public ChatMessageDtoValidator()
    {
        RuleFor(x => x.Role).IsInEnum();
        RuleFor(x => x.TextContent).NotEmpty().MaximumLength(50_000);
    }
}

internal class SendChatMessageContextInfoDtoValidator : AbstractValidator<SendChatMessageContextInfoDto>
{
    public SendChatMessageContextInfoDtoValidator()
    {
        RuleFor(x => x.NovelId).NotEmpty();
        RuleFor(x => x.ChapterIndex).GreaterThanOrEqualTo(0).When(x => x.ChapterIndex.HasValue);
        RuleFor(x => x.UserMessage).NotEmpty().MaximumLength(50_000);
        RuleForEach(x => x.PreviousMessages).SetValidator(new ChatMessageDtoValidator());
        RuleForEach(x => x.CompendiumIds).NotEmpty();
        RuleForEach(x => x.CompendiumRecordIds).NotEmpty();
    }
}

internal class DescribeCompendiumImageContextInfoDtoValidator : AbstractValidator<DescribeCompendiumImageContextInfoDto>
{
    public DescribeCompendiumImageContextInfoDtoValidator()
    {
        RuleFor(x => x.CompendiumId).NotEmpty();
        RuleFor(x => x.Instructions).MaximumLength(5_000);
    }
}

internal class DescribeImageContextInfoDtoValidator : AbstractValidator<DescribeImageContextInfoDto>
{
    public DescribeImageContextInfoDtoValidator()
    {
        RuleFor(x => x.Instructions).MaximumLength(5_000);
    }
}

internal class CreateCompendiumRecordImageGenerationPromptContextInfoDtoValidator
    : AbstractValidator<CreateCompendiumRecordImageGenerationPromptContextInfoDto>
{
    public CreateCompendiumRecordImageGenerationPromptContextInfoDtoValidator()
    {
        RuleFor(x => x.CompendiumId).NotEmpty();
        RuleFor(x => x.CompendiumRecordId).NotEmpty();
        RuleFor(x => x.Instructions).MaximumLength(5_000);
    }
}

internal class CreateStoryEventsContextInfoDtoValidator : AbstractValidator<CreateStoryEventsContextInfoDto>
{
    public CreateStoryEventsContextInfoDtoValidator()
    {
        RuleFor(x => x.NovelId).NotEmpty();
        RuleFor(x => x.ChapterIndex).GreaterThanOrEqualTo(0);
    }
}

internal class PrepareImmersiveTtsContextInfoDtoValidator : AbstractValidator<PrepareImmersiveTtsContextInfoDto>
{
    public PrepareImmersiveTtsContextInfoDtoValidator()
    {
        RuleFor(x => x.NovelId).NotEmpty();
        RuleFor(x => x.ChapterIndex).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SectionIndex).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Provider).IsInEnum();
        RuleFor(x => x.TtsModelId).NotEmpty().MaximumLength(200);
    }
}

internal class TranslateNovelContextInfoDtoValidator : AbstractValidator<TranslateNovelContextInfoDto>
{
    public TranslateNovelContextInfoDtoValidator()
    {
        RuleFor(x => x.NovelId).NotEmpty();
        RuleFor(x => x.ChapterIndex).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TargetLanguage).IsInEnum();
        RuleFor(x => x.Instructions).MaximumLength(5_000);
    }
}
