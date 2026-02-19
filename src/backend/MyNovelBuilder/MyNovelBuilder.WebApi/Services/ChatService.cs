using System.Text.Json;
using System.Text.Json.Serialization;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Models.Chats;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for chat functionality.
/// </summary>
public class ChatService : IChatService
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    
    /// <summary></summary>
    public ChatService()
    {
        _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        _jsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    }

    private static string GetChatsDirectoryPath() => 
        Path.Combine(Globals.DataFolder, "chats");
    
    private static string GetChatFilePath(Guid id) =>
        Path.Combine(GetChatsDirectoryPath(), $"{id}.json");

    /// <inheritdoc/>
    public async Task<Chat> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var path = GetChatFilePath(id);

        if (!File.Exists(path))
        {
            throw new ApiException(
                ErrorCodes.ChatNotFound, $"Chat with ID {id} was not found.");
        }
        
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<Chat>(json, _jsonSerializerOptions)!;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<ChatMetadata>> GetAllMetadataAsync(CancellationToken cancellationToken = default)
    {
        var path = GetChatsDirectoryPath();
        Directory.CreateDirectory(path);
        
        var metadataList = new List<ChatMetadata>();
        
        foreach (var file in Directory.GetFiles(path, "*.json"))
        {
            var json = await File.ReadAllTextAsync(file, cancellationToken);
            var chat = JsonSerializer.Deserialize<Chat>(json, _jsonSerializerOptions)!;
            var metadata = new ChatMetadata
            {
                Id = Path.GetFileNameWithoutExtension(file) is { } fileName
                    ? Guid.Parse(fileName)
                    : Guid.Empty,
                CreatedAt = chat.CreatedAt,
                UpdatedAt = chat.UpdatedAt,
                Name = chat.Name
            };
            metadataList.Add(metadata);
        }
        
        return metadataList
            .OrderByDescending(m => m.UpdatedAt)
            .ToList();
    }

    /// <inheritdoc/>
    public async Task CreateAsync(Chat chat, CancellationToken cancellationToken = default)
    {
        var path = GetChatFilePath(chat.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        
        var json = JsonSerializer.Serialize(chat, _jsonSerializerOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(Guid id, Chat chat, CancellationToken cancellationToken = default)
    {
        var path = GetChatFilePath(id);
        
        if (!File.Exists(path))
        {
            throw new ApiException(
                ErrorCodes.ChatNotFound, $"Chat with ID {id} was not found.");
        }
        
        var json = JsonSerializer.Serialize(chat, _jsonSerializerOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    /// <inheritdoc/>
    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var path = GetChatFilePath(id);
        
        if (!File.Exists(path))
        {
            throw new ApiException(
                ErrorCodes.ChatNotFound, $"Chat with ID {id} was not found.");
        }
        
        File.Delete(path);
        return Task.CompletedTask;
    }
}
