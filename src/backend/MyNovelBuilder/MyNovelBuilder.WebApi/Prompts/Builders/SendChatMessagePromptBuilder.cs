using System.Text;
using MyNovelBuilder.WebApi.Dtos.Generate;

namespace MyNovelBuilder.WebApi.Prompts.Builders;

/// <summary>
/// A system prompt builder for chats.
/// </summary>
public class SendChatMessagePromptBuilder : NovelPromptBuilder<SendChatMessageContextInfoDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SendChatMessagePromptBuilder"/> class.
    /// </summary>
    public SendChatMessagePromptBuilder(string prompt) : base(prompt)
    {
        
    }

    /// <inheritdoc />
    public override NovelPromptBuilder<SendChatMessageContextInfoDto> ReplacePlaceholders(
        NovelPromptBuilderContext<SendChatMessageContextInfoDto> context)
    {
        base.ReplacePlaceholders(context);
        
        var contextString = context.Client.ChapterIndex.HasValue
            ? PromptBuilderUtils.GetWholeChapter(context.Prose, context.Client.ChapterIndex.Value)
            : PromptBuilderUtils.GetEntireStory(context.Prose);

        var recordsInContext = context.CompendiumRecords
            .Where(r => context.Client.CompendiumIds.Contains(r.Compendium.Id)
                        || context.Client.CompendiumRecordIds.Contains(r.Id));
        var recordsInContextList = recordsInContext.ToList();
        PromptBuilderUtils.TrackIncludedRecords(
            context.IncludedCompendiumRecordIds,
            recordsInContextList);
        
        Builder
            .Replace("{{context}}", contextString)
            .Replace("{{chatHistory}}", BuildChatHistory(context.Client.PreviousMessages))
            .Replace("{{instructions}}", context.Client.UserMessage)
            .Replace("{{records}}", PromptBuilderUtils.CreateCompendiumRecordsString(
                recordsInContextList, (
                    context.Prose,
                    context.Client.ChapterIndex,
                    null)));
        
        return this;
    }

    private static string BuildChatHistory(IEnumerable<ChatMessageDto> previousMessages)
    {
        var builder = new StringBuilder();
        
        foreach (var message in previousMessages)
        {
            builder.AppendLine($"{message.Role}: {message.TextContent}");
            builder.AppendLine();
        }
        
        return builder.ToString();
    }
}
