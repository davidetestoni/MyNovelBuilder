using MyNovelBuilder.WebApi.Models.Chats;

namespace MyNovelBuilder.WebApi.Dtos.Chat;

/// <summary>
/// Data transfer object for updating a chat.
/// </summary>
public class UpdateChatDto
{
    /// <summary>
    /// The name of the chat.
    /// </summary>
    public string? Name { get; set; }
    
    /// <summary>
    /// The index of the chapter associated with the chat context.
    /// If null, the context will include the entire novel.
    /// </summary>
    public int? ChapterIndex { get; set; }
    
    /// <summary>
    /// The ids of the compendia included in the chat context.
    /// All records from these compendia are considered when generating responses.
    /// The compendia must belong to the specified novel.
    /// </summary>
    public IEnumerable<Guid> CompendiumIds { get; set; } = [];

    /// <summary>
    /// The ids of the specific compendium records included in the chat context.
    /// The records must belong to compendia associated with the specified novel.
    /// </summary>
    public IEnumerable<Guid> CompendiumRecordIds { get; set; } = [];
    
    /// <summary>
    /// The messages in the chat conversation.
    /// </summary>
    public IEnumerable<ChatMessage> Messages { get; init; } = [];
}