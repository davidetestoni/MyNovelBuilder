using MyNovelBuilder.WebApi.Models.Chats;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for chat functionality.
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Get a chat by its ID.
    /// </summary>
    Task<Chat> GetByIdAsync(Guid id);
    
    /// <summary>
    /// Get metadata for all chats.
    /// </summary>
    Task<IEnumerable<ChatMetadata>> GetAllMetadataAsync();
    
    /// <summary>
    /// Create a chat.
    /// </summary>
    Task CreateAsync(Chat chat);
    
    /// <summary>
    /// Update a chat.
    /// </summary>
    Task UpdateAsync(Guid id, Chat chat);
    
    /// <summary>
    /// Delete a chat by its ID.
    /// </summary>
    Task DeleteAsync(Guid id);
}