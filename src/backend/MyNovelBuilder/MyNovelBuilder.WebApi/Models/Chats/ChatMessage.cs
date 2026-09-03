using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Models.Chats;

/// <summary>
/// Represents a message in a chat conversation.
/// </summary>
public class ChatMessage
{
    /// <summary>
    /// The unique identifier of the chat message.
    /// </summary>
    public required Guid Id { get; set; }
    
    /// <summary>
    /// The time the message was sent.
    /// </summary>
    public required DateTime SentAt { get; set; }
    
    /// <summary>
    /// The role of the sender of the chat message.
    /// </summary>
    public required ChatMessageRole Role { get; set; }
    
    /// <summary>
    /// The text content of the chat message.
    /// </summary>
    public required string TextContent { get; set;  }

    /// <summary>
    /// Optional raw structured output associated with an assistant message.
    /// </summary>
    public string? StructuredContent { get; set; }
}
