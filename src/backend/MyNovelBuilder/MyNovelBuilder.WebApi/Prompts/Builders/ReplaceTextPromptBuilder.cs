using MyNovelBuilder.WebApi.Dtos.Generate;

namespace MyNovelBuilder.WebApi.Prompts.Builders;

/// <summary>
/// A prompt builder for text replacement.
/// </summary>
public class ReplaceTextPromptBuilder : PromptBuilder<ReplaceTextContextInfoDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReplaceTextPromptBuilder"/> class.
    /// </summary>
    public ReplaceTextPromptBuilder(string prompt) : base(prompt)
    {
        
    }

    /// <inheritdoc />
    public override PromptBuilder<ReplaceTextContextInfoDto> ReplacePlaceholders(
        PromptBuilderContext<ReplaceTextContextInfoDto> context)
    {
        base.ReplacePlaceholders(context);
        
        var chapter = GetChapter(context.Prose, context.Client.ChapterIndex);
        var section = GetSection(chapter, context.Client.SectionIndex);
        var text = StripHtmlTags(section.Text);
        
        var textToReplace = text.Substring(context.Client.TextOffset,
            context.Client.TextLength);

        var recordsInContext = FilterRecordsInContext(context.CompendiumRecords, textToReplace);
        
        // If there are instructions, also search for records in them
        if (!string.IsNullOrWhiteSpace(context.Client.Instructions))
        {
            recordsInContext.UnionWith(
                FilterRecordsInContext(context.CompendiumRecords, context.Client.Instructions));
        }
        
        Builder
            .Replace("{{instructions}}", context.Client.Instructions ?? string.Empty)
            .Replace("{{textToReplace}}", textToReplace)
            .Replace("{{records}}", CreateCompendiumRecordsString(recordsInContext.ToList()));
        
        return this;
    }
}
