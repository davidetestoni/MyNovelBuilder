using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Extensions;

namespace MyNovelBuilder.WebApi.Prompts.Builders;

/// <summary>
/// A prompt builder for text summarization.
/// </summary>
public class SummarizeTextPromptBuilder : NovelPromptBuilder<SummarizeTextContextInfoDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SummarizeTextPromptBuilder"/> class.
    /// </summary>
    public SummarizeTextPromptBuilder(string prompt) : base(prompt)
    {
        
    }

    /// <inheritdoc />
    public override NovelPromptBuilder<SummarizeTextContextInfoDto> ReplacePlaceholders(
        NovelPromptBuilderContext<SummarizeTextContextInfoDto> context)
    {
        base.ReplacePlaceholders(context);

        var chapter = PromptBuilderUtils.GetChapter(context.Prose, context.Client.ChapterIndex);
        var section = PromptBuilderUtils.GetSection(chapter, context.Client.SectionIndex);
        var sectionText = section?.Text ?? string.Empty;
        
        var recordsInContext = PromptBuilderUtils.FilterRecordsInContext(
            context.CompendiumRecords, sectionText.StripHtml());
        var recordsInContextList = recordsInContext.ToList();
        PromptBuilderUtils.TrackIncludedRecords(
            context.IncludedCompendiumRecordIds,
            recordsInContextList);
        
        Builder
            .Replace("{{context}}", sectionText.StripHtml())
            .Replace("{{records}}", PromptBuilderUtils.CreateCompendiumRecordsString(
                recordsInContextList, (
                    context.Prose,
                    context.Client.ChapterIndex,
                    context.Client.SectionIndex)));

        return this;
    }
}
