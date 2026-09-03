namespace MyNovelBuilder.WebApi.Models.Prompts;

/// <summary>
/// The result of processing a prompt, including the messages to send
/// to the text generation service and any compendium records that
/// have been included in the prompt context.
/// </summary>
public class ProcessedPrompt
{
    /// <summary>
    /// The messages to send to the text generation service, in order.
    /// </summary>
    public required IEnumerable<PromptMessage> Messages { get; init; }
    
    /// <summary>
    /// The IDs of any compendium records that have been included in the prompt context.
    /// </summary>
    public required IEnumerable<Guid> IncludedCompendiumRecordIds { get; init; }
}