using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Extensions;

namespace MyNovelBuilder.WebApi.Prompts.Builders;

/// <summary>
/// A prompt builder for text replacement.
/// </summary>
public class ReplaceTextPromptBuilder : NovelPromptBuilder<ReplaceTextContextInfoDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReplaceTextPromptBuilder"/> class.
    /// </summary>
    public ReplaceTextPromptBuilder(string prompt) : base(prompt)
    {
        
    }

    /// <inheritdoc />
    public override NovelPromptBuilder<ReplaceTextContextInfoDto> ReplacePlaceholders(
        NovelPromptBuilderContext<ReplaceTextContextInfoDto> context)
    {
        base.ReplacePlaceholders(context);
        
        var chapter = PromptBuilderUtils.GetChapter(context.Prose, context.Client.ChapterIndex);
        var section = PromptBuilderUtils.GetSection(chapter, context.Client.SectionIndex);
        var text = section?.Text.StripHtml() ?? string.Empty;
        
        var textBefore = text[..context.Client.TextOffset].StripHtml();
        var textAfter = text[(context.Client.TextOffset + context.Client.TextLength)..].StripHtml();
        
        var textToReplace = text.Substring(context.Client.TextOffset,
            context.Client.TextLength);

        var recordsInContext = PromptBuilderUtils.FilterRecordsInContext(
            context.CompendiumRecords,
            textToReplace);
        recordsInContext.UnionWith(
            PromptBuilderUtils.FilterRecordsInContext(context.CompendiumRecords, textBefore));
        recordsInContext.UnionWith(
            PromptBuilderUtils.FilterRecordsInContext(context.CompendiumRecords, textAfter));
        
        // If there are instructions, also search for records in them
        if (!string.IsNullOrWhiteSpace(context.Client.Instructions))
        {
            recordsInContext.UnionWith(
                PromptBuilderUtils.FilterRecordsInContext(
                    context.CompendiumRecords,
                    context.Client.Instructions));
        }

        var recordsInContextList = recordsInContext.ToList();
        PromptBuilderUtils.TrackIncludedRecords(
            context.IncludedCompendiumRecordIds,
            recordsInContextList);
        
        Builder
            .Replace("{{textBefore}}", textBefore)
            .Replace("{{textAfter}}", textAfter)
            .Replace("{{instructions}}", context.Client.Instructions ?? string.Empty)
            .Replace("{{textToReplace}}", textToReplace)
            .Replace("{{records}}", PromptBuilderUtils.CreateCompendiumRecordsString(
                recordsInContextList, (
                    context.Prose,
                    context.Client.ChapterIndex,
                    context.Client.SectionIndex)));
        
        return this;
    }
}
