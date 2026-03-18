using MyNovelBuilder.WebApi.Dtos.Generate;

namespace MyNovelBuilder.WebApi.Prompts.Builders;

/// <summary>
/// A prompt builder for generic image description.
/// </summary>
public class DescribeImagePromptBuilder : GenericPromptBuilder<DescribeImageContextInfoDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DescribeImagePromptBuilder"/> class.
    /// </summary>
    public DescribeImagePromptBuilder(string prompt) : base(prompt)
    {
    }

    /// <inheritdoc />
    public override GenericPromptBuilder<DescribeImageContextInfoDto> ReplacePlaceholders(
        GenericPromptBuilderContext<DescribeImageContextInfoDto> context)
    {
        Builder.Replace("{{instructions}}", context.Client.Instructions ?? string.Empty);
        return this;
    }
}
