namespace MyNovelBuilder.WebApi.Models.Chats;

/// <summary>
/// Represents the context of a chat conversation.
/// </summary>
public class ChatContext
{
    /// <summary>
    /// The id of the novel associated with the chat context.
    /// </summary>
    public Guid NovelId { get; set; }
    
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
    public IList<Guid> CompendiumIds { get; set; } = new List<Guid>();
    
    /// <summary>
    /// The ids of the specific compendium records included in the chat context.
    /// The records must belong to compendia associated with the specified novel.
    /// </summary>
    public IList<Guid> CompendiumRecordIds { get; set; } = new List<Guid>();
}