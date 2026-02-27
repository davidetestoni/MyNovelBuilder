using MyNovelBuilder.WebApi.Dtos.Integrations;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Models.Integrations;
using MyNovelBuilder.WebApi.Services;
using MyNovelBuilder.WebApi.Tests.Factories;
using Xunit.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace MyNovelBuilder.WebApi.Tests.Integration.Controllers;

public class IntegrationsControllerIntegrationTests(
    TestWebApplicationFactory<Program> factory,
    ITestOutputHelper output)
    : ControllerIntegrationTests(factory, output), IAsyncLifetime
{
    private IIntegrationsService IntegrationsService => Factory.Services.GetRequiredService<IIntegrationsService>();

    public async Task InitializeAsync()
    {
        await ResetDbAsync();
    }

    [Fact]
    public async Task GetIntegrationsConfig_ReturnsOk()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var config = new IntegrationsConfig
        {
            OpenRouterApiKey = "test-api-key",
            NanoGptApiKey = "nano-gpt-api-key",
            TextGenerationProvider = TextGenerationProvider.OpenRouter,
            TtsProvider = TtsProvider.PocketTts,
            TtsVoiceId = "test-voice-id"
        };
        await IntegrationsService.UpdateConfigAsync(config);

        // Act
        var result = await GetJsonAsync<IntegrationsConfigDto>(
            client, "api/integrations/config");

        // Assert
        Assert.True(result.IsOk);
        var dto = result.Value;
        Assert.True(dto.HasOpenRouterApiKey);
        Assert.False(dto.HasGoogleGenAiApiKey);
        Assert.True(dto.HasNanoGptApiKey);
        Assert.Equal(TextGenerationProvider.OpenRouter, dto.TextGenerationProvider);
        Assert.Equal(TtsProvider.PocketTts, dto.TtsProvider);
        Assert.Equal(config.TtsVoiceId, dto.TtsVoiceId);
    }

    [Fact]
    public async Task UpdateIntegrationsConfig_ReturnsNoContent()
    {
        // Arrange
        using var client = Factory.CreateClient();
        var config = new IntegrationsConfig
        {
            OpenRouterApiKey = "test-api-key",
            TextGenerationProvider = TextGenerationProvider.OpenRouter,
            TtsProvider = TtsProvider.PocketTts,
            TtsVoiceId = "test-voice-id"
        };
        await IntegrationsService.UpdateConfigAsync(config);
        var updateDto = new UpdateIntegrationsConfigDto
        {
            OpenRouterApiKey = "new-api-key",
            NanoGptApiKey = "new-nano-gpt-api-key",
            TextGenerationProvider = TextGenerationProvider.OpenRouter,
            TtsProvider = TtsProvider.Kokoro,
            TtsVoiceId = "new-voice"
        };

        // Act
        var result = await PutJsonAsync<object>(
            client, "api/integrations/config", updateDto);

        // Assert
        Assert.Null(result.Error);
        
        var updatedConfig = await IntegrationsService.GetConfigAsync();
        Assert.Equal(updateDto.OpenRouterApiKey, updatedConfig.OpenRouterApiKey);
        Assert.Equal(updateDto.NanoGptApiKey, updatedConfig.NanoGptApiKey);
        Assert.Equal(updateDto.TextGenerationProvider, updatedConfig.TextGenerationProvider);
        Assert.Equal(updateDto.TtsProvider, updatedConfig.TtsProvider);
        Assert.Equal(updateDto.TtsVoiceId, updatedConfig.TtsVoiceId);
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}
