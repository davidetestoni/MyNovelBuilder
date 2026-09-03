using System.Text;

namespace MyNovelBuilder.WebApi.Prompts.Builders;

/// <summary>
/// Shared base for all prompt builders.
/// </summary>
public abstract class PromptBuilderBase
{
    /// <summary>
    /// The backing string builder.
    /// </summary>
    protected readonly StringBuilder Builder;

    /// <summary>
    /// Creates a new prompt builder.
    /// </summary>
    protected PromptBuilderBase(string prompt)
    {
        Builder = new StringBuilder(prompt);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Builder.ToString();
    }
}
