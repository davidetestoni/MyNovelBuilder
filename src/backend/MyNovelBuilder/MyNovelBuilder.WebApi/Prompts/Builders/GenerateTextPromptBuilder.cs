using MyNovelBuilder.WebApi.Dtos.Generate;

namespace MyNovelBuilder.WebApi.Prompts.Builders;

/// <summary>
/// A prompt builder for text generation.
/// </summary>
public class GenerateTextPromptBuilder : NovelPromptBuilder<GenerateTextContextInfoDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateTextPromptBuilder"/> class.
    /// </summary>
    public GenerateTextPromptBuilder(string prompt) : base(prompt)
    {
        
    }

    /// <inheritdoc />
    public override NovelPromptBuilder<GenerateTextContextInfoDto> ReplacePlaceholders(
        NovelPromptBuilderContext<GenerateTextContextInfoDto> context)
    {
        base.ReplacePlaceholders(context);
        
        var contextString = PromptBuilderUtils.GetStorySoFar(
            context.Prose, context.Client.ChapterIndex,
            context.Client.SectionIndex, context.Client.TextOffset);
        
        var recordsInContext = PromptBuilderUtils.FilterRecordsInContext(
            context.CompendiumRecords,
            contextString);
        
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
            .Replace("{{context}}", contextString)
            .Replace("{{instructions}}", context.Client.Instructions ?? string.Empty)
            .Replace("{{records}}", PromptBuilderUtils.CreateCompendiumRecordsString(
                recordsInContextList, (
                    context.Prose,
                    context.Client.ChapterIndex,
                    context.Client.SectionIndex)));
        
        return this;
    }
}
