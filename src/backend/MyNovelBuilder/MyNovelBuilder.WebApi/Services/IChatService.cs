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
    Task<Chat> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get metadata for all chats.
    /// </summary>
    Task<IEnumerable<ChatMetadata>> GetAllMetadataAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create a chat.
    /// </summary>
    Task CreateAsync(Chat chat, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update a chat.
    /// </summary>
    Task UpdateAsync(Guid id, Chat chat, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete a chat by its ID.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
