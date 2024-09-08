using System.Text;
using MyNovelBuilder.WebApi.Dtos.Generate;

namespace MyNovelBuilder.WebApi.Prompts.Builders;

/// <summary>
/// A prompt builder for text generation.
/// </summary>
public class GenerateTextPromptBuilder : PromptBuilder<GenerateTextContextInfoDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateTextPromptBuilder"/> class.
    /// </summary>
    public GenerateTextPromptBuilder(string prompt) : base(prompt)
    {
        
    }

    /// <inheritdoc />
    public override PromptBuilder<GenerateTextContextInfoDto> ReplacePlaceholders(
        PromptBuilderContext<GenerateTextContextInfoDto> context)
    {
        base.ReplacePlaceholders(context);
        
        var chapter = GetChapter(context.Prose, context.Client.ChapterIndex);
        var section = GetSection(chapter, context.Client.SectionIndex);
        var text = HtmlToText(section.Text);
        
        // Get up to 6 previous sections (even across chapters)
        var previousSections = context.Prose.Chapters
            .SelectMany(c => c.Sections)
            .TakeWhile(s => s != section)
            .TakeLast(6)
            .ToList();

        var contextBuilder = new StringBuilder();
        
        // Append the summaries of the previous sections except the last one (up to 5)
        foreach (var previousSection in previousSections)
        {
            contextBuilder.Append(HtmlToText(previousSection.Text));
            contextBuilder.Append("\n\n");
        }
        
        // Append the text of the last previous section (if any)
        if (previousSections.Count != 0)
        {
            contextBuilder.Append(HtmlToText(previousSections[^1].Text));
            contextBuilder.Append("\n\n");
        }
        
        // Append the text of the current section, up to the offset
        contextBuilder.Append(text[..context.Client.TextOffset]);

        var contextString = contextBuilder.ToString();
        var recordsInContext = FilterRecordsInContext(context.CompendiumRecords, contextString);
        
        // If there are instructions, also search for records in them
        if (!string.IsNullOrWhiteSpace(context.Client.Instructions))
        {
            recordsInContext.UnionWith(
                FilterRecordsInContext(context.CompendiumRecords, context.Client.Instructions));
        }
        
        Builder
            .Replace("{{context}}", contextString)
            .Replace("{{instructions}}", context.Client.Instructions ?? string.Empty)
            .Replace("{{records}}", CreateCompendiumRecordsString(recordsInContext.ToList()));
        
        return this;
    }
}
