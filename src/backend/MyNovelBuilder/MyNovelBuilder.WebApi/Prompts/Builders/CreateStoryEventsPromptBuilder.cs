using MyNovelBuilder.WebApi.Dtos.Generate;

namespace MyNovelBuilder.WebApi.Prompts.Builders;

/// <summary>
/// A prompt builder for creating story events.
/// </summary>
public class CreateStoryEventsPromptBuilder : PromptBuilder<CreateStoryEventsContextInfoDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateStoryEventsPromptBuilder"/> class.
    /// </summary>
    public CreateStoryEventsPromptBuilder(string prompt) : base(prompt)
    {
    }

    /// <inheritdoc />
    public override PromptBuilder<CreateStoryEventsContextInfoDto> ReplacePlaceholders(
        PromptBuilderContext<CreateStoryEventsContextInfoDto> context)
    {
        base.ReplacePlaceholders(context);

        var contextString = GetWholeChapter(context.Prose, context.Client.ChapterIndex);

        var recordsInContext = FilterRecordsInContext(context.CompendiumRecords, contextString);

        Builder
            .Replace("{{context}}", contextString)
            .Replace("{{records}}", CreateCompendiumRecordsString(
                recordsInContext.ToList(),
                (
                    context.Prose,
                    context.Client.ChapterIndex,
                    null
                )));

        return this;
    }
}
