using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Services;
using MyNovelBuilder.WebApi.Tests.Factories;
using Xunit.Abstractions;

namespace MyNovelBuilder.WebApi.Tests.Integration.Controllers;

public class GenerateAudioControllerIntegrationTests(
    TestWebApplicationFactory<Program> factory,
    ITestOutputHelper output)
    : ControllerIntegrationTests(factory, output), IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await ResetDbAsync();
    }

    [Fact]
    public async Task DebugImmersiveTts_ReturnsResolvedChunks()
    {
        await using var customFactory = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IImmersiveTtsService>();
                services.AddSingleton<IImmersiveTtsService>(new FakeImmersiveTtsService());
            });
        });
        using var client = customFactory.CreateClient();

        var result = await PostJsonAsync<ImmersiveTtsDebugResponseDto>(
            client,
            "api/generate/audio/tts/immersive/debug",
            new ImmersiveTtsRequestDto
            {
                NovelId = Guid.NewGuid(),
                PromptId = Guid.NewGuid(),
                ChapterIndex = 0,
                SectionIndex = 0,
                Provider = TtsProvider.ElevenLabs
            });

        Assert.True(result.IsOk);
        Assert.Equal(TtsProvider.ElevenLabs, result.Value.Provider);
        Assert.Single(result.Value.Chunks);
        Assert.Equal("narrator", result.Value.Chunks.Single().SpeakerKind);
    }

    [Fact]
    public async Task GenerateImmersiveAudioStream_ReturnsWavResponse()
    {
        await using var customFactory = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IImmersiveTtsService>();
                services.AddSingleton<IImmersiveTtsService>(new FakeImmersiveTtsService());
            });
        });
        using var client = customFactory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "api/generate/audio/tts/immersive/stream",
            new ImmersiveTtsRequestDto
            {
                NovelId = Guid.NewGuid(),
                PromptId = Guid.NewGuid(),
                ChapterIndex = 0,
                SectionIndex = 0
            });

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("audio/wav", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal([1, 2, 3, 4], await response.Content.ReadAsByteArrayAsync());
    }

    [Theory]
    [InlineData("promptId")]
    [InlineData("chapterIndex")]
    [InlineData("sectionIndex")]
    public async Task DebugImmersiveTts_WithMissingRequiredValue_ReturnsBadRequest(
        string missingProperty)
    {
        using var client = Factory.CreateClient();
        var request = new Dictionary<string, object>
        {
            ["novelId"] = Guid.NewGuid(),
            ["promptId"] = Guid.NewGuid(),
            ["chapterIndex"] = 0,
            ["sectionIndex"] = 0
        };
        request.Remove(missingProperty);

        var response = await client.PostAsJsonAsync(
            "api/generate/audio/tts/immersive/debug",
            request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    private sealed class FakeImmersiveTtsService : IImmersiveTtsService
    {
        public Task<ImmersiveTtsDebugResponseDto> PrepareDebugAsync(
            ImmersiveTtsRequestDto request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ImmersiveTtsDebugResponseDto
            {
                Provider = request.Provider ?? TtsProvider.ElevenLabs,
                TtsModelId = "eleven-v3",
                TextGenerationModelId = "openrouter/planner-model",
                PauseMs = 150,
                Chunks =
                [
                    new ImmersiveTtsDebugChunkDto
                    {
                        Sequence = 0,
                        SpeakerKind = "narrator",
                        SpeakerName = "Narrator",
                        VoiceId = "narrator-voice",
                        Text = "Test line"
                    }
                ]
            });
        }

        public Task<Stream> GenerateStreamAsync(
            ImmersiveTtsRequestDto request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Stream>(new MemoryStream([1, 2, 3, 4]));
        }
    }
}
