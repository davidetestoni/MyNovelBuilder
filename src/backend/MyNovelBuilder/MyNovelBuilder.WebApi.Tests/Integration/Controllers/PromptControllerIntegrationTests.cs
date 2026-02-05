using Microsoft.Extensions.DependencyInjection;
using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.Prompt;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Models.Prompts;
using MyNovelBuilder.WebApi.Tests.Factories;
using Xunit.Abstractions;

namespace MyNovelBuilder.WebApi.Tests.Integration.Controllers;

public class PromptControllerIntegrationTests(
    TestWebApplicationFactory<Program> factory,
    ITestOutputHelper output)
    : ControllerIntegrationTests(factory, output), IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await ResetDbAsync();
    }

    [Fact]
    public async Task GetPrompt_ReturnsOk()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var prompt = new Prompt
        {
            Name = "Test Prompt",
            Type = PromptType.GenerateText,
            Messages = new List<PromptMessage>
            {
                new() { Role = PromptMessageRole.System, Message = "You are a helpful assistant." },
                new() { Role = PromptMessageRole.User, Message = "Write a story." }
            }
        };
        UnitOfWork.Prompts.Add(prompt);
        await UnitOfWork.SaveChangesAsync();
        
        // Act
        var result = await GetJsonAsync<PromptDto>(
            client, $"api/prompt/{prompt.Id}");
        
        // Assert
        Assert.True(result.IsOk);
        var dto = result.Value;
        Assert.Equal(prompt.Name, dto.Name);
        Assert.Equal(prompt.Type, dto.Type);
        Assert.Equal(2, dto.Messages.Count());
    }

    [Fact]
    public async Task GetAllPrompts_ReturnsOk()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var prompt = new Prompt
        {
            Name = "Test Prompt",
            Type = PromptType.GenerateText,
            Messages = new List<PromptMessage>
            {
                new() { Role = PromptMessageRole.System, Message = "You are a helpful assistant." }
            }
        };
        UnitOfWork.Prompts.Add(prompt);
        await UnitOfWork.SaveChangesAsync();
        
        // Act
        var result = await GetJsonAsync<IEnumerable<PromptDto>>(
            client, "api/prompts");
        
        // Assert
        Assert.True(result.IsOk);
        var dtos = result.Value.ToList();
        Assert.Single(dtos);
        Assert.Equal(prompt.Name, dtos[0].Name);
    }

    [Fact]
    public async Task CreatePrompt_ReturnsOk()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var createDto = new CreatePromptDto
        {
            Name = "New Prompt",
            Type = PromptType.SendChatMessage,
            Messages = new List<PromptMessageDto>
            {
                new() { Role = PromptMessageRole.System, Message = "System message" }
            }
        };
        
        // Act
        var result = await PostJsonAsync<PromptDto>(
            client, "api/prompt", createDto);
        
        // Assert
        Assert.True(result.IsOk);
        var dto = result.Value;
        Assert.Equal(createDto.Name, dto.Name);
        Assert.Equal(createDto.Type, dto.Type);
        Assert.Single(dto.Messages);
    }
    
    [Fact]
    public async Task UpdatePrompt_ReturnsOk()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var prompt = new Prompt
        {
            Name = "Old Prompt",
            Type = PromptType.GenerateText,
            Messages = new List<PromptMessage>
            {
                new() { Role = PromptMessageRole.System, Message = "Old message" }
            }
        };
        UnitOfWork.Prompts.Add(prompt);
        await UnitOfWork.SaveChangesAsync();
        
        var updateDto = new UpdatePromptDto
        {
            Id = prompt.Id,
            Name = "Updated Prompt",
            Type = PromptType.SendChatMessage,
            Messages = new List<PromptMessageDto>
            {
                new() { Role = PromptMessageRole.System, Message = "New message" }
            }
        };
        
        // Act
        var result = await PutJsonAsync<PromptDto>(
            client, "api/prompt", updateDto);
        
        // Assert
        Assert.True(result.IsOk);
        var dto = result.Value;
        Assert.Equal(updateDto.Name, dto.Name);
        Assert.Equal(updateDto.Type, dto.Type);
        Assert.Equal("New message", dto.Messages.First().Message);
    }
    
    [Fact]
    public async Task DeletePrompt_ReturnsOk()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var prompt = new Prompt
        {
            Name = "Delete Me",
            Type = PromptType.GenerateText,
            Messages = new List<PromptMessage>()
        };
        UnitOfWork.Prompts.Add(prompt);
        await UnitOfWork.SaveChangesAsync();
        
        // Act
        var error = await DeleteAsync(
            client, $"api/prompt/{prompt.Id}");
        
        // Assert
        Assert.Null(error);
        
        using var scope = Factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var deletedPrompt = await uow.Prompts.GetByIdAsync(prompt.Id);
        Assert.Null(deletedPrompt);
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}