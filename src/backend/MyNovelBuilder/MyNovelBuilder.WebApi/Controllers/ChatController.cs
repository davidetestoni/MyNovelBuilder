using Microsoft.AspNetCore.Mvc;
using MyNovelBuilder.WebApi.Dtos.Chat;
using MyNovelBuilder.WebApi.Models.Chats;
using MyNovelBuilder.WebApi.Services;

namespace MyNovelBuilder.WebApi.Controllers;

/// <summary>
/// Controller for chats.
/// </summary>
[Route("api/chat")]
[ApiController]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;

    /// <summary></summary>
    public ChatController(IChatService chatService)
    {
        _chatService = chatService;
    }
    
    /// <summary>
    /// Get metadata for all chats.
    /// </summary>
    [HttpGet("/api/chats")]
    public async Task<IEnumerable<ChatMetadata>> GetAllChatMetadata(CancellationToken cancellationToken = default)
    {
        return await _chatService.GetAllMetadataAsync(cancellationToken);
    }
    
    /// <summary>
    /// Get a chat by its ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<Chat> GetChatById(Guid id, CancellationToken cancellationToken = default)
    {
        return await _chatService.GetByIdAsync(id, cancellationToken);
    }

    /// <summary>
    /// Create a chat.
    /// </summary>
    [HttpPost]
    public async Task<Chat> CreateChat(CreateChatDto dto, CancellationToken cancellationToken = default)
    {
        var chat = new Chat
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Name = null,
            Context = new ChatContext
            {
                NovelId = dto.NovelId
            }
        };
        
        await _chatService.CreateAsync(chat, cancellationToken);
        return chat;
    }

    /// <summary>
    /// Update a chat.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task UpdateChat(Guid id, UpdateChatDto dto, CancellationToken cancellationToken = default)
    { 
        var chat = await _chatService.GetByIdAsync(id, cancellationToken);
        chat.UpdatedAt = DateTime.UtcNow;
        chat.Name = dto.Name;
        chat.Context.ChapterIndex = dto.ChapterIndex;
        chat.Context.CompendiumIds = dto.CompendiumIds.ToList();
        chat.Context.CompendiumRecordIds = dto.CompendiumRecordIds.ToList();
        chat.Messages = dto.Messages.ToList();
        
        await _chatService.UpdateAsync(id, chat, cancellationToken);
    }

    /// <summary>
    /// Delete a chat by its ID.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task DeleteChat(Guid id, CancellationToken cancellationToken = default)
    {
        await _chatService.DeleteAsync(id, cancellationToken);
    }
}
