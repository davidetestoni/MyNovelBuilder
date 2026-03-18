using MyNovelBuilder.WebApi.Dtos.Generate;

namespace MyNovelBuilder.WebApi.Prompts.Builders;

/// <summary>
/// Base class for novel-scoped prompt builders.
/// </summary>
public abstract class NovelPromptBuilder<T> : PromptBuilderBase
    where T : NovelTextGenerationContextInfoDto
{
    /// <summary>
    /// Creates a new prompt builder.
    /// </summary>
    protected NovelPromptBuilder(string prompt) : base(prompt)
    {
    }

    /// <summary>
    /// Replaces placeholders in the prompt with information from the context.
    /// </summary>
    public virtual NovelPromptBuilder<T> ReplacePlaceholders(
        NovelPromptBuilderContext<T> context)
    {
        Builder
            .Replace("{{novel.language}}", context.Novel.Language.ToString())
            .Replace("{{novel.pov}}", PromptBuilderUtils.CreateNovelPovString(context.Novel))
            .Replace("{{novel.tense}}", $"{context.Novel.Tense} tense");

        return this;
    }
}
