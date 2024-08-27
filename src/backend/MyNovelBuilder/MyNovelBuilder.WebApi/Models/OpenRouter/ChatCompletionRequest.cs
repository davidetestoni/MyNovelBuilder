namespace MyNovelBuilder.WebApi.Models.OpenRouter;

/// <summary>
/// A chat completion request on OpenRouter.
/// </summary>
public class ChatCompletionRequest
{
    /// <summary>
    /// The model to use for completion.
    /// </summary>
    public required string Model { get; set; }
    
    /// <summary>
    /// The messages to use for completion.
    /// </summary>
    public required IEnumerable<ChatCompletionMessage> Messages { get; set; }
}

/// <summary>
/// A chat completion message on OpenRouter.
/// </summary>
public class ChatCompletionMessage
{
    /// <summary>
    /// The role of the message.
    /// </summary>
    public required ChatCompletionMessageRole Role { get; set; }
    
    /// <summary>
    /// The content of the message.
    /// </summary>
    public required string Content { get; set; }
}

/// <summary>
/// The role of a chat completion message.
/// </summary>
public enum ChatCompletionMessageRole
{
    /// <summary>
    /// A message that is sent as the user.
    /// </summary>
    User,
    
    /// <summary>
    /// A message that is sent as the assistant.
    /// </summary>
    Assistant,
    
    /// <summary>
    /// A system message.
    /// </summary>
    System,
    
    /// <summary>
    /// A message that is sent as a tool.
    /// </summary>
    Tool
}
