namespace MyNovelBuilder.WebApi.Models.Chats;

/// <summary>
/// Represents metadata about a chat conversation.
/// </summary>
public class ChatMetadata
{
    /// <summary>
    /// The chat's ID.
    /// </summary>
    public required Guid Id { get; set; }

    /// <summary>
    /// The associated novel's ID.
    /// </summary>
    public required Guid NovelId { get; set; }
    
    /// <summary>
    /// The time the chat was created.
    /// </summary>
    public required DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// The time the chat was last updated.
    /// </summary>
    public required DateTime UpdatedAt { get; set; }
    
    /// <summary>
    /// The chat's name. Null at first, then gets auto-generated
    /// from the first user message.
    /// </summary>
    public string? Name { get; set;  }
}
