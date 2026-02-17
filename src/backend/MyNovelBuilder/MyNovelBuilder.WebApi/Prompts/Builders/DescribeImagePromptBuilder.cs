using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.Generate;

namespace MyNovelBuilder.WebApi.Prompts.Builders;

/// <summary>
/// A prompt builder for image description.
/// </summary>
public class DescribeImagePromptBuilder : PromptBuilder<DescribeImageContextInfoDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DescribeImagePromptBuilder"/> class.
    /// </summary>
    public DescribeImagePromptBuilder(string prompt) : base(prompt)
    {
    }

    /// <inheritdoc />
    public override PromptBuilder<DescribeImageContextInfoDto> ReplacePlaceholders(
        PromptBuilderContext<DescribeImageContextInfoDto> context)
    {
        Builder
            .Replace("{{instructions}}", context.Client.Instructions ?? string.Empty)
            .Replace("{{records}}", CreateCompendiumRecordsString(context.CompendiumRecords));

        return this;
    }
}
