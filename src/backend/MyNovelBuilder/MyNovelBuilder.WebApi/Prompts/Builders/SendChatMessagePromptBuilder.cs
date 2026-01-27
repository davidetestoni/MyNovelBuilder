using MyNovelBuilder.WebApi.Dtos.Generate;

namespace MyNovelBuilder.WebApi.Prompts.Builders;

/// <summary>
/// A system prompt builder for chats.
/// </summary>
public class SendChatMessagePromptBuilder : PromptBuilder<SendChatMessageContextInfoDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SendChatMessagePromptBuilder"/> class.
    /// </summary>
    public SendChatMessagePromptBuilder(string prompt) : base(prompt)
    {
        
    }

    /// <inheritdoc />
    public override PromptBuilder<SendChatMessageContextInfoDto> ReplacePlaceholders(
        PromptBuilderContext<SendChatMessageContextInfoDto> context)
    {
        base.ReplacePlaceholders(context);
        
        var contextString = context.Client.ChapterIndex.HasValue
            ? GetWholeChapter(context.Prose, context.Client.ChapterIndex.Value)
            : GetEntireStory(context.Prose);

        var recordsInContext = context.CompendiumRecords
            .Where(r => context.Client.CompendiumIds.Contains(r.Compendium.Id)
                        || context.Client.CompendiumRecordIds.Contains(r.Id));
        
        Builder
            .Replace("{{context}}", DecodeHtmlEntities(contextString))
            .Replace("{{instructions}}", context.Client.UserMessage)
            .Replace("{{records}}", CreateCompendiumRecordsString(recordsInContext.ToList()));
        
        return this;
    }
}