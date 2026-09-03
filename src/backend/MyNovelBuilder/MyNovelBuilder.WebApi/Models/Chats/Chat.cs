namespace MyNovelBuilder.WebApi.Models.Chats;

/// <summary>
/// Represents a chat conversation.
/// </summary>
public class Chat
{
    /// <summary>
    /// The chat's ID.
    /// </summary>
    public required Guid Id { get; init; }
    
    /// <summary>
    /// The time the chat was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }
    
    /// <summary>
    /// The time the chat was last updated.
    /// </summary>
    public required DateTime UpdatedAt { get; set; }
    
    /// <summary>
    /// The chat's name. Null at first, then gets auto-generated
    /// from the first user message.
    /// </summary>
    public string? Name { get; set;  }
    
    /// <summary>
    /// The context of the chat conversation.
    /// </summary>
    public required ChatContext Context { get; init; }
    
    /// <summary>
    /// The messages in the chat conversation.
    /// </summary>
    public IList<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}