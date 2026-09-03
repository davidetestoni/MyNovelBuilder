using MyNovelBuilder.WebApi.Dtos.Generate;

namespace MyNovelBuilder.WebApi.Prompts.Builders;

/// <summary>
/// Base class for compendium-scoped prompt builders.
/// </summary>
public abstract class CompendiumPromptBuilder<T> : PromptBuilderBase
    where T : CompendiumTextGenerationContextInfoDto
{
    /// <summary>
    /// Creates a new prompt builder.
    /// </summary>
    protected CompendiumPromptBuilder(string prompt) : base(prompt)
    {
    }

    /// <summary>
    /// Replaces placeholders in the prompt with information from the context.
    /// </summary>
    public virtual CompendiumPromptBuilder<T> ReplacePlaceholders(
        CompendiumPromptBuilderContext<T> context)
    {
        return this;
    }
}
