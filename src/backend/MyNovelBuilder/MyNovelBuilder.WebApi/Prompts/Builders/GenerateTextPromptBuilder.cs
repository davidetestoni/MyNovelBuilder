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
        
        var contextString = GetStorySoFar(
            context.Prose, context.Client.ChapterIndex,
            context.Client.SectionIndex, context.Client.TextOffset);
        
        var recordsInContext = FilterRecordsInContext(context.CompendiumRecords, contextString);
        
        // If there are instructions, also search for records in them
        if (!string.IsNullOrWhiteSpace(context.Client.Instructions))
        {
            recordsInContext.UnionWith(
                FilterRecordsInContext(context.CompendiumRecords, context.Client.Instructions));
        }
        
        Builder
            .Replace("{{context}}", DecodeHtmlEntities(contextString))
            .Replace("{{instructions}}", context.Client.Instructions ?? string.Empty)
            .Replace("{{records}}", CreateCompendiumRecordsString(recordsInContext.ToList()));
        
        return this;
    }
}
