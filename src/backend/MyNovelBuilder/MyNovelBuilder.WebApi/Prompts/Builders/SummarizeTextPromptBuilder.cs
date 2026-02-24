using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Extensions;

namespace MyNovelBuilder.WebApi.Prompts.Builders;

/// <summary>
/// A prompt builder for text summarization.
/// </summary>
public class SummarizeTextPromptBuilder : PromptBuilder<SummarizeTextContextInfoDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SummarizeTextPromptBuilder"/> class.
    /// </summary>
    public SummarizeTextPromptBuilder(string prompt) : base(prompt)
    {
        
    }

    /// <inheritdoc />
    public override PromptBuilder<SummarizeTextContextInfoDto> ReplacePlaceholders(
        PromptBuilderContext<SummarizeTextContextInfoDto> context)
    {
        base.ReplacePlaceholders(context);

        var chapter = GetChapter(context.Prose, context.Client.ChapterIndex);
        var section = GetSection(chapter, context.Client.SectionIndex);
        var sectionText = section?.Text ?? string.Empty;
        
        var recordsInContext = FilterRecordsInContext(
            context.CompendiumRecords, sectionText.StripHtml());
        var recordsInContextList = recordsInContext.ToList();
        TrackIncludedRecords(context, recordsInContextList);
        
        Builder
            .Replace("{{context}}", sectionText.StripHtml())
            .Replace("{{records}}", CreateCompendiumRecordsString(
                recordsInContextList, (
                    context.Prose,
                    context.Client.ChapterIndex,
                    context.Client.SectionIndex)));

        return this;
    }
}
