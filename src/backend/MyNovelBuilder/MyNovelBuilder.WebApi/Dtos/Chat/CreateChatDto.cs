namespace MyNovelBuilder.WebApi.Dtos.Chat;

/// <summary>
/// Data transfer object for creating a chat.
/// </summary>
public class CreateChatDto
{
    /// <summary>
    /// The ID of the novel associated with the chat.
    /// </summary>
    public required Guid NovelId { get; set; }
}