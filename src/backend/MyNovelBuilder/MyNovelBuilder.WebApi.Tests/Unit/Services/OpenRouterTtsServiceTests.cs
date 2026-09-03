using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using MyNovelBuilder.WebApi.Models.Integrations;
using MyNovelBuilder.WebApi.Models.Tts;
using MyNovelBuilder.WebApi.Services;
using MyNovelBuilder.WebApi.Services.Tts;

namespace MyNovelBuilder.WebApi.Tests.Unit.Services;

public class OpenRouterTtsServiceTests
{
    [Fact]
    public async Task GetModelsAsync_RequestsSpeechModels_AndMapsSupportedVoices()
    {
        var handler = new RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "data": [
                        {
                          "id": "hexgrad/kokoro-82m",
                          "name": "hexgrad: Kokoro 82M",
                          "architecture": {
                            "output_modalities": ["speech"]
                          },
                          "supported_voices": ["af_alloy", "if_sara"]
                        },
                        {
                          "id": "example/text-model",
                          "name": "Text Model",
                          "architecture": {
                            "output_modalities": ["text"]
                          }
                        }
                      ]
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });
        var service = CreateService(handler);

        var models = (await service.GetModelsAsync()).ToList();

        var model = Assert.Single(models);
        Assert.Equal("hexgrad/kokoro-82m", model.ModelId);
        Assert.Equal("hexgrad: Kokoro 82M", model.Name);
        Assert.Equal(
            new[] { "af_alloy", "if_sara" },
            model.Voices.Select(voice => voice.VoiceId));

        var request = Assert.Single(handler.Requests);
        Assert.Equal(
            "https://openrouter.ai/api/v1/models?output_modalities=speech",
            request.RequestUri?.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("test-openrouter-key", request.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task GenerateAudioAsync_PostsToOpenRouterSpeechEndpoint()
    {
        var handler = new RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([0x00, 0x00, 0x01, 0x00])
            });
        var service = CreateService(handler);

        var audio = await service.GenerateAudioAsync(new TtsRequest
        {
            Message = "Hello",
            ModelId = "hexgrad/kokoro-82m",
            VoiceId = "af_alloy"
        });

        Assert.NotEmpty(audio);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(
            "https://openrouter.ai/api/v1/audio/speech",
            request.RequestUri?.ToString());

        using var requestJson = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
        Assert.Equal("Hello", requestJson.RootElement.GetProperty("input").GetString());
        Assert.Equal("hexgrad/kokoro-82m", requestJson.RootElement.GetProperty("model").GetString());
        Assert.Equal("af_alloy", requestJson.RootElement.GetProperty("voice").GetString());
        Assert.Equal("pcm", requestJson.RootElement.GetProperty("response_format").GetString());
    }

    private static OpenRouterTtsService CreateService(RecordingHttpMessageHandler handler)
    {
        return new OpenRouterTtsService(
            new HttpClient(handler),
            NullLogger<OpenRouterTtsService>.Instance,
            new FakeIntegrationsService(new IntegrationsConfig
            {
                OpenRouterApiKey = "test-openrouter-key"
            }));
    }

    private sealed class FakeIntegrationsService(IntegrationsConfig config) : IIntegrationsService
    {
        public ValueTask<IntegrationsConfig> GetConfigAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(config);

        public Task UpdateConfigAsync(
            IntegrationsConfig config,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(CloneRequest(request));
            return Task.FromResult(response);
        }

        private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri);

            foreach (var header in request.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            if (request.Content is null)
            {
                return clone;
            }

            var contentBytes = request.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            clone.Content = new ByteArrayContent(contentBytes);

            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return clone;
        }
    }
}
