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
            TextGenerationModelId = "openrouter/gpt-test",
            TtsProvider = TtsProvider.PocketTts,
            VideoGenerationProvider = VideoGenerationProvider.DeApi,
            TtsModelId = "test-model-id",
            TtsVoiceId = "test-voice-id",
            TtsEnableTextEmphasis = true,
            TtsEnableImmersive = true,
            TtsImmersivePauseMs = 325
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
        Assert.Equal(config.TextGenerationModelId, dto.TextGenerationModelId);
        Assert.Equal(TtsProvider.PocketTts, dto.TtsProvider);
        Assert.Equal(VideoGenerationProvider.DeApi, dto.VideoGenerationProvider);
        Assert.Equal(config.TtsModelId, dto.TtsModelId);
        Assert.Equal(config.TtsVoiceId, dto.TtsVoiceId);
        Assert.True(dto.TtsEnableTextEmphasis);
        Assert.True(dto.TtsEnableImmersive);
        Assert.Equal(325, dto.TtsImmersivePauseMs);
    }

    [Fact]
    public async Task GetIntegrationsConfig_WhenConfigDoesNotExist_UsesDefaultTextGenerationModel()
    {
        // Arrange
        using var client = Factory.CreateClient();

        // Act
        var result = await GetJsonAsync<IntegrationsConfigDto>(
            client, "api/integrations/config");

        // Assert
        Assert.True(result.IsOk);
        Assert.Equal(
            IntegrationsConfig.DefaultTextGenerationModelId,
            result.Value.TextGenerationModelId);
        Assert.False(result.Value.TtsEnableImmersive);
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
            TextGenerationModelId = "openrouter/old-model",
            TtsProvider = TtsProvider.PocketTts,
            VideoGenerationProvider = VideoGenerationProvider.DeApi,
            TtsModelId = "test-model-id",
            TtsVoiceId = "test-voice-id",
            TtsEnableTextEmphasis = false
        };
        await IntegrationsService.UpdateConfigAsync(config);
        var updateDto = new UpdateIntegrationsConfigDto
        {
            OpenRouterApiKey = "new-api-key",
            NanoGptApiKey = "new-nano-gpt-api-key",
            TextGenerationProvider = TextGenerationProvider.OpenRouter,
            TextGenerationModelId = "openrouter/new-model",
            TtsProvider = TtsProvider.Kokoro,
            VideoGenerationProvider = VideoGenerationProvider.DeApi,
            TtsModelId = "new-model",
            TtsVoiceId = "new-voice",
            TtsEnableTextEmphasis = true,
            TtsEnableImmersive = true,
            TtsImmersivePauseMs = 480
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
        Assert.Equal(updateDto.TextGenerationModelId, updatedConfig.TextGenerationModelId);
        Assert.Equal(updateDto.TtsProvider, updatedConfig.TtsProvider);
        Assert.Equal(updateDto.VideoGenerationProvider, updatedConfig.VideoGenerationProvider);
        Assert.Equal(updateDto.TtsModelId, updatedConfig.TtsModelId);
        Assert.Equal(updateDto.TtsVoiceId, updatedConfig.TtsVoiceId);
        Assert.Equal(updateDto.TtsEnableTextEmphasis, updatedConfig.TtsEnableTextEmphasis);
        Assert.Equal(updateDto.TtsEnableImmersive, updatedConfig.TtsEnableImmersive);
        Assert.Equal(480, updatedConfig.TtsImmersivePauseMs);
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}
