﻿using MyNovelBuilder.WebApi.Dtos.Generate;

namespace MyNovelBuilder.WebApi.Prompts.Builders;

/// <summary>
/// A prompt builder for text summarization.
/// </summary>
public class SummarizeTextPromptBuilder : PromptBuilder<SummarizeTextContextInfoDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SummarizeTextPromptBuilder"/> class.
    /// </summary>
    public SummarizeTextPromptBuilder(string prompt) : base(prompt)
    {
        
    }

    /// <inheritdoc />
    public override PromptBuilder<SummarizeTextContextInfoDto> ReplacePlaceholders(
        PromptBuilderContext<SummarizeTextContextInfoDto> context)
    {
        base.ReplacePlaceholders(context);

        var chapter = GetChapter(context.Prose, context.Client.ChapterIndex);
        var section = GetSection(chapter, context.Client.SectionIndex);
        
        Builder
            .Replace("{{context}}", HtmlToText(section.Text));

        return this;
    }
}