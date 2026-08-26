using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using MyNovelBuilder.WebApi.Models.Integrations;
using MyNovelBuilder.WebApi.Models.Tts;
using MyNovelBuilder.WebApi.Services;
using MyNovelBuilder.WebApi.Services.Tts;

namespace MyNovelBuilder.WebApi.Tests.Unit.Services;

public class UnrealSpeechTtsServiceTests
{
    [Fact]
    public async Task GenerateAudioAsync_UsesManagedClientWithoutLeakingAuthorizationToOutputUri()
    {
        var handler = new QueueingHttpMessageHandler(
        [
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "SynthesisTask": {
                        "OutputUri": "https://storage.test/audio.mp3"
                      }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1, 2, 3, 4])
            }
        ]);
        var service = new UnrealSpeechTtsService(
            NullLogger<UnrealSpeechTtsService>.Instance,
            new HttpClient(handler),
            new FakeIntegrationsService());

        var audio = await service.GenerateAudioAsync(new TtsRequest
        {
            Message = "Test narration",
            VoiceId = "Autumn"
        });

        Assert.Equal([1, 2, 3, 4], audio);
        Assert.Collection(
            handler.Requests,
            request =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal(
                    "https://api.v8.unrealspeech.com/synthesisTasks",
                    request.RequestUri?.ToString());
                Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
                Assert.Equal("test-unrealspeech-key", request.Headers.Authorization?.Parameter);
            },
            request =>
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Equal("https://storage.test/audio.mp3", request.RequestUri?.ToString());
                Assert.Null(request.Headers.Authorization);
            });
    }

    private sealed class FakeIntegrationsService : IIntegrationsService
    {
        public ValueTask<IntegrationsConfig> GetConfigAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new IntegrationsConfig
            {
                UnrealSpeechApiKey = "test-unrealspeech-key"
            });

        public Task UpdateConfigAsync(
            IntegrationsConfig config,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class QueueingHttpMessageHandler(
        IEnumerable<HttpResponseMessage> responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri);
            foreach (var header in request.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            Requests.Add(clone);
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
