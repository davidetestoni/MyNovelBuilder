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
    public async Task<IEnumerable<ChatMetadata>> GetAllChatMetadata()
    {
        return await _chatService.GetAllMetadataAsync();
    }
    
    /// <summary>
    /// Get a chat by its ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<Chat> GetChatById(Guid id)
    {
        return await _chatService.GetByIdAsync(id);
    }

    /// <summary>
    /// Create a chat.
    /// </summary>
    [HttpPost]
    public async Task<Chat> CreateChat(CreateChatDto dto)
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
        
        await _chatService.CreateAsync(chat);
        return chat;
    }

    /// <summary>
    /// Update a chat.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task UpdateChat(Guid id, UpdateChatDto dto)
    { 
        var chat = await _chatService.GetByIdAsync(id);
        chat.UpdatedAt = DateTime.UtcNow;
        chat.Name = dto.Name;
        chat.Context.ChapterIndex = dto.ChapterIndex;
        chat.Context.CompendiumIds = dto.CompendiumIds.ToList();
        chat.Context.CompendiumRecordIds = dto.CompendiumRecordIds.ToList();
        chat.Messages = dto.Messages.ToList();
        
        await _chatService.UpdateAsync(id, chat);
    }

    /// <summary>
    /// Delete a chat by its ID.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task DeleteChat(Guid id)
    {
        await _chatService.DeleteAsync(id);
    }
}