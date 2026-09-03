using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Extensions;

namespace MyNovelBuilder.WebApi.Prompts.Builders;

/// <summary>
/// A prompt builder for creating a compendium record.
/// </summary>
public class CreateCompendiumRecordPromptBuilder
    : NovelPromptBuilder<CreateCompendiumRecordContextInfoDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateCompendiumRecordPromptBuilder"/> class.
    /// </summary>
    /// <param name="prompt"></param>
    public CreateCompendiumRecordPromptBuilder(string prompt) : base(prompt)
    {
        
    }

    /// <inheritdoc />
    public override NovelPromptBuilder<CreateCompendiumRecordContextInfoDto> ReplacePlaceholders(
        NovelPromptBuilderContext<CreateCompendiumRecordContextInfoDto> context)
    {
        base.ReplacePlaceholders(context);
        
        var chapter = PromptBuilderUtils.GetChapter(context.Prose, context.Client.ChapterIndex);
        var section = PromptBuilderUtils.GetSection(chapter, context.Client.SectionIndex);
        var text = section?.Text.StripHtml() ?? string.Empty;
        
        var recordDetails = text.Substring(context.Client.TextOffset,
            context.Client.TextLength);
        
        var contextString = PromptBuilderUtils.GetStorySoFar(
            context.Prose, context.Client.ChapterIndex,
            context.Client.SectionIndex, context.Client.TextOffset);
        
        var recordsInContext = PromptBuilderUtils.FilterRecordsInContext(
            context.CompendiumRecords, contextString);
        
        // If there are instructions, also search for records in them
        if (!string.IsNullOrWhiteSpace(context.Client.Instructions))
        {
            recordsInContext.UnionWith(
                PromptBuilderUtils.FilterRecordsInContext(
                    context.CompendiumRecords,
                    context.Client.Instructions));
        }
        
        recordsInContext.UnionWith(
            PromptBuilderUtils.FilterRecordsInContext(
                context.CompendiumRecords,
                recordDetails));
        var recordsInContextList = recordsInContext.ToList();
        PromptBuilderUtils.TrackIncludedRecords(
            context.IncludedCompendiumRecordIds,
            recordsInContextList);
        
        Builder
            .Replace("{{context}}", contextString)
            .Replace("{{instructions}}", context.Client.Instructions ?? string.Empty)
            .Replace("{{recordDetails}}", recordDetails)
            .Replace("{{records}}", PromptBuilderUtils.CreateCompendiumRecordsString(
                recordsInContextList, (
                    context.Prose,
                    context.Client.ChapterIndex,
                    context.Client.SectionIndex)));

        return this;
    }
}
