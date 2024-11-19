using MyNovelBuilder.WebApi.Dtos.Generate;

namespace MyNovelBuilder.WebApi.Prompts.Builders;

/// <summary>
/// A prompt builder for creating a compendium record.
/// </summary>
public class CreateCompendiumRecordPromptBuilder : PromptBuilder<CreateCompendiumRecordContextInfoDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateCompendiumRecordPromptBuilder"/> class.
    /// </summary>
    /// <param name="prompt"></param>
    public CreateCompendiumRecordPromptBuilder(string prompt) : base(prompt)
    {
        
    }

    /// <inheritdoc />
    public override PromptBuilder<CreateCompendiumRecordContextInfoDto> ReplacePlaceholders(
        PromptBuilderContext<CreateCompendiumRecordContextInfoDto> context)
    {
        base.ReplacePlaceholders(context);
        
        var chapter = GetChapter(context.Prose, context.Client.ChapterIndex);
        var section = GetSection(chapter, context.Client.SectionIndex);
        var text = StripHtmlTags(section.Text);
        
        var recordDetails = text.Substring(context.Client.TextOffset,
            context.Client.TextLength);
        
        var contextString = GetStorySoFar(
            context.Prose, context.Client.ChapterIndex,
            context.Client.SectionIndex, context.Client.TextOffset);
        
        var recordsInContext = FilterRecordsInContext(
            context.CompendiumRecords, contextString);
        
        // If there are instructions, also search for records in them
        if (!string.IsNullOrWhiteSpace(context.Client.Instructions))
        {
            recordsInContext.UnionWith(
                FilterRecordsInContext(context.CompendiumRecords, context.Client.Instructions));
        }
        
        recordsInContext.UnionWith(
            FilterRecordsInContext(context.CompendiumRecords, recordDetails));
        
        Builder
            .Replace("{{context}}", DecodeHtmlEntities(contextString))
            .Replace("{{instructions}}", context.Client.Instructions ?? string.Empty)
            .Replace("{{recordDetails}}", recordDetails)
            .Replace("{{records}}", CreateCompendiumRecordsString(recordsInContext.ToList()));

        return this;
    }
}
