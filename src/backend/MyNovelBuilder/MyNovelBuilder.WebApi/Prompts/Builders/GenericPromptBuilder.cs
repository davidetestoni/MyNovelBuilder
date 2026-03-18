using MyNovelBuilder.WebApi.Dtos.Generate;

namespace MyNovelBuilder.WebApi.Prompts.Builders;

/// <summary>
/// Base class for generic prompt builders.
/// </summary>
public abstract class GenericPromptBuilder<T> : PromptBuilderBase
    where T : GenericTextGenerationContextInfoDto
{
    /// <summary>
    /// Creates a new prompt builder.
    /// </summary>
    protected GenericPromptBuilder(string prompt) : base(prompt)
    {
    }

    /// <summary>
    /// Replaces placeholders in the prompt with information from the context.
    /// </summary>
    public virtual GenericPromptBuilder<T> ReplacePlaceholders(
        GenericPromptBuilderContext<T> context)
    {
        return this;
    }
}
