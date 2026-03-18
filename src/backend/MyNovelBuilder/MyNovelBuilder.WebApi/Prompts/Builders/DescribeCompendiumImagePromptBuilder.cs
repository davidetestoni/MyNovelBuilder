using MyNovelBuilder.WebApi.Dtos.Generate;

namespace MyNovelBuilder.WebApi.Prompts.Builders;

/// <summary>
/// A prompt builder for compendium-scoped image description.
/// </summary>
public class DescribeCompendiumImagePromptBuilder
    : CompendiumPromptBuilder<DescribeCompendiumImageContextInfoDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DescribeCompendiumImagePromptBuilder"/> class.
    /// </summary>
    public DescribeCompendiumImagePromptBuilder(string prompt) : base(prompt)
    {
    }

    /// <inheritdoc />
    public override CompendiumPromptBuilder<DescribeCompendiumImageContextInfoDto> ReplacePlaceholders(
        CompendiumPromptBuilderContext<DescribeCompendiumImageContextInfoDto> context)
    {
        PromptBuilderUtils.TrackIncludedRecords(
            context.IncludedCompendiumRecordIds,
            context.CompendiumRecords);

        Builder
            .Replace("{{instructions}}", context.Client.Instructions ?? string.Empty)
            .Replace("{{records}}", PromptBuilderUtils.CreateCompendiumRecordsString(
                context.CompendiumRecords));

        return this;
    }
}
