using System.Text.Json;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Extensions;

namespace MyNovelBuilder.WebApi.Prompts.Builders;

/// <summary>
/// A prompt builder for suggesting story developments.
/// </summary>
public class SuggestStoryDevelopmentsPromptBuilder
    : NovelPromptBuilder<SuggestStoryDevelopmentsContextInfoDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SuggestStoryDevelopmentsPromptBuilder"/> class.
    /// </summary>
    public SuggestStoryDevelopmentsPromptBuilder(string prompt) : base(prompt)
    {
    }

    /// <inheritdoc />
    public override NovelPromptBuilder<SuggestStoryDevelopmentsContextInfoDto> ReplacePlaceholders(
        NovelPromptBuilderContext<SuggestStoryDevelopmentsContextInfoDto> context)
    {
        base.ReplacePlaceholders(context);

        var chapter = PromptBuilderUtils.GetChapter(context.Prose, context.Client.ChapterIndex);
        var section = PromptBuilderUtils.GetSection(chapter, context.Client.SectionIndex);
        var contextString = PromptBuilderUtils.GetStorySoFar(
            context.Prose,
            context.Client.ChapterIndex,
            context.Client.SectionIndex,
            context.Client.TextOffset);

        var recordsInContext = PromptBuilderUtils.FilterRecordsInContext(
            context.CompendiumRecords,
            contextString);

        if (!string.IsNullOrWhiteSpace(section?.Summary))
        {
            recordsInContext.UnionWith(
                PromptBuilderUtils.FilterRecordsInContext(
                    context.CompendiumRecords,
                    section.Summary));
        }

        var recordsInContextList = recordsInContext.ToList();
        PromptBuilderUtils.TrackIncludedRecords(
            context.IncludedCompendiumRecordIds,
            recordsInContextList);

        Builder
            .Replace("{{context}}", contextString)
            .Replace("{{currentChapterTitle}}", chapter.Title.StripHtml())
            .Replace(
                "{{currentChapterEvents}}",
                JsonSerializer.Serialize(chapter.StoryEvents ?? []))
            .Replace("{{sectionSummary}}", section?.Summary ?? string.Empty)
            .Replace("{{records}}", PromptBuilderUtils.CreateCompendiumRecordsString(
                recordsInContextList,
                (
                    context.Prose,
                    context.Client.ChapterIndex,
                    context.Client.SectionIndex
                )));

        return this;
    }
}
