using MyNovelBuilder.WebApi.Dtos.Chat;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Models.Chats;
using MyNovelBuilder.WebApi.Services;
using MyNovelBuilder.WebApi.Tests.Factories;
using Xunit.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using MyNovelBuilder.WebApi.Exceptions;

namespace MyNovelBuilder.WebApi.Tests.Integration.Controllers;

public class ChatControllerIntegrationTests(
    TestWebApplicationFactory<Program> factory,
    ITestOutputHelper output)
    : ControllerIntegrationTests(factory, output), IAsyncLifetime
{
    private IChatService ChatService => Factory.Services.GetRequiredService<IChatService>();

    public async Task InitializeAsync()
    {
        await ResetDbAsync();
    }

    [Fact]
    public async Task GetAllChatMetadata_ReturnsOk()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var chat = new Chat
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Name = "Test Chat",
            Context = new ChatContext { NovelId = Guid.NewGuid() }
        };
        await ChatService.CreateAsync(chat);

        // Act
        var result = await GetJsonAsync<IEnumerable<ChatMetadata>>(
            client, "api/chats");

        // Assert
        Assert.True(result.IsOk);
        var metadata = result.Value.ToList();
        Assert.Single(metadata);
        Assert.Equal(chat.Id, metadata[0].Id);
        Assert.Equal(chat.Context.NovelId, metadata[0].NovelId);
        Assert.Equal(chat.Name, metadata[0].Name);
    }

    [Fact]
    public async Task GetChatById_ReturnsOk()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var chat = new Chat
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Name = "Test Chat",
            Context = new ChatContext { NovelId = Guid.NewGuid() }
        };
        await ChatService.CreateAsync(chat);

        // Act
        var result = await GetJsonAsync<Chat>(
            client, $"api/chat/{chat.Id}");

        // Assert
        Assert.True(result.IsOk);
        Assert.Equal(chat.Id, result.Value.Id);
        Assert.Equal(chat.Name, result.Value.Name);
    }

    [Fact]
    public async Task CreateChat_ReturnsOk()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var novelId = Guid.NewGuid();
        var createDto = new CreateChatDto
        {
            NovelId = novelId
        };

        // Act
        var result = await PostJsonAsync<Chat>(
            client, "api/chat", createDto);

        // Assert
        Assert.True(result.IsOk);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
        Assert.Equal(novelId, result.Value.Context.NovelId);
    }

    [Fact]
    public async Task UpdateChat_ReturnsOk()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var chat = new Chat
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Name = "Original Name",
            Context = new ChatContext { NovelId = Guid.NewGuid() }
        };
        await ChatService.CreateAsync(chat);

        var updateDto = new UpdateChatDto
        {
            Name = "Updated Name",
            ChapterIndex = 1,
            CompendiumIds = [Guid.NewGuid()],
            CompendiumRecordIds = [Guid.NewGuid()],
            Messages =
            [
                new ChatMessage
                {
                    Id = Guid.NewGuid(),
                    Role = ChatMessageRole.User,
                    TextContent = "Hello",
                    SentAt = DateTime.UtcNow
                }
            ]
        };

        // Act
        var error = await PutJsonAsync<object>(
            client, $"api/chat/{chat.Id}", updateDto);

        // Assert
        Assert.Null(error.Error);
        
        var updatedChat = await ChatService.GetByIdAsync(chat.Id);
        Assert.Equal(updateDto.Name, updatedChat.Name);
        Assert.Equal(updateDto.ChapterIndex, updatedChat.Context.ChapterIndex);
        Assert.Equal(updateDto.CompendiumIds, updatedChat.Context.CompendiumIds);
        Assert.Equal(updateDto.CompendiumRecordIds, updatedChat.Context.CompendiumRecordIds);
        Assert.Single(updatedChat.Messages);
        Assert.Equal(updateDto.Messages.First().TextContent, updatedChat.Messages[0].TextContent);
    }

    [Fact]
    public async Task DeleteChat_ReturnsOk()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var chat = new Chat
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Name = "Test Chat",
            Context = new ChatContext { NovelId = Guid.NewGuid() }
        };
        await ChatService.CreateAsync(chat);

        // Act
        var error = await DeleteAsync(
            client, $"api/chat/{chat.Id}");

        // Assert
        Assert.Null(error);
        await Assert.ThrowsAsync<ApiException>(
            () => ChatService.GetByIdAsync(chat.Id));
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}
