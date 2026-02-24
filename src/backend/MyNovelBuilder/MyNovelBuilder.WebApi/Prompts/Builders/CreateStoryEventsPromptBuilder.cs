using System.Text.Json;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Models.Novels;

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
        var previousChapter = context.Client.ChapterIndex > 0
            ? GetChapter(context.Prose, context.Client.ChapterIndex - 1)
            : null;
        var nextChapter = context.Client.ChapterIndex < context.Prose.Chapters.Count - 1
            ? GetChapter(context.Prose, context.Client.ChapterIndex + 1)
            : null;

        var recordsInContext = FilterRecordsInContext(context.CompendiumRecords, contextString);
        var recordsInContextList = recordsInContext.ToList();
        TrackIncludedRecords(context, recordsInContextList);

        Builder
            .Replace("{{context}}", contextString)
            .Replace("{{previousChapterEvents}}", SerializeStoryEvents(previousChapter))
            .Replace("{{nextChapterEvents}}", SerializeStoryEvents(nextChapter))
            .Replace("{{records}}", CreateCompendiumRecordsString(
                recordsInContextList,
                (
                    context.Prose,
                    context.Client.ChapterIndex,
                    null
                )));

        return this;
    }

    private static string SerializeStoryEvents(Chapter? chapter)
    {
        return JsonSerializer.Serialize(chapter?.StoryEvents ?? []);
    }
}
