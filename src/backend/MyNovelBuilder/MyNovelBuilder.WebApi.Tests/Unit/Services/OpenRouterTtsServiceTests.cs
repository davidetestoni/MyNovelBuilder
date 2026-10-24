using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Models.Integrations;
using MyNovelBuilder.WebApi.Models.Tts;
using MyNovelBuilder.WebApi.Services;
using MyNovelBuilder.WebApi.Services.Tts;
using NAudio.Wave;

namespace MyNovelBuilder.WebApi.Tests.Unit.Services;

public class OpenRouterTtsServiceTests
{
    [Fact]
    public async Task GetModelsAsync_DoesNotInventVoicesWhenMetadataIsUnknown()
    {
        var service = CreateService(new RecordingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {"data":[
                  {"id":"fish-audio/s2.1-pro","architecture":{"output_modalities":["speech"]},"supported_voices":null},
                  {"id":"future/missing","architecture":{"output_modalities":["speech"]}},
                  {"id":"future/empty","architecture":{"output_modalities":["speech"]},"supported_voices":[]}
                ]}
                """)
        }));

        var models = (await service.GetModelsAsync()).ToList();
        Assert.Equal(3, models.Count);
        Assert.All(models, model => Assert.Empty(model.Voices));
    }

    [Fact]
    public async Task GenerateAudioAsync_LogsRequestContextWhenProviderOmitsExplanation()
    {
        const string body = """{"error":{"message":"Provider returned 400","code":400}}""";
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(body) };
        response.Headers.Add("X-Generation-Id", "gen-test");
        response.Headers.Add("X-Request-Id", "request-test");
        response.Headers.Add("CF-Ray", "ray-test");
        var logger = new RecordingLogger();
        var service = CreateService(new RecordingHttpMessageHandler(response), logger);

        var error = await Assert.ThrowsAsync<ApiException>(() => service.GenerateAudioAsync(new TtsRequest
        {
            Message = "Private manuscript à", ModelId = "fish-audio/s2.1-pro", VoiceId = "alloy"
        }));

        Assert.Contains("no further provider explanation", error.Message);
        var log = Assert.Single(logger.Messages);
        Assert.Contains("fish-audio/s2.1-pro", log);
        Assert.Contains("alloy", log);
        Assert.Contains("mp3", log);
        Assert.Contains("Input characters: 20", log);
        Assert.Contains("Input UTF-8 bytes: 21", log);
        Assert.Contains("gen-test", log);
        Assert.Contains("request-test", log);
        Assert.Contains("ray-test", log);
        Assert.Contains(body, log);
        Assert.DoesNotContain("Private manuscript", log);
        Assert.DoesNotContain("test-openrouter-key", log);
    }

    [Theory]
    [InlineData("\"Voice reference not found\"")]
    [InlineData("{\"message\":\"Voice reference not found\"}")]
    [InlineData("\"{\\\"error\\\":{\\\"message\\\":\\\"Voice reference not found\\\"}}\"")]
    public async Task GenerateAudioAsync_IncludesProviderErrorDetailsWhenReturned(string raw)
    {
        var logger = new RecordingLogger();
        var service = CreateService(new RecordingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"error\":{\"message\":\"Provider returned 400\",\"metadata\":{\"raw\":" + raw + "}}}")
        }), logger);
        var error = await Assert.ThrowsAsync<ApiException>(() => service.GenerateAudioStreamAsync(new TtsRequest
        {
            Message = "Hello", ModelId = "fish-audio/s2.1-pro", VoiceId = "custom-reference"
        }));
        Assert.Contains("Voice reference not found", error.Message);
        Assert.Contains("Voice reference not found", Assert.Single(logger.Messages));
    }

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
                Content = new ByteArrayContent(await ReadToneAsync(44100, 1))
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
        Assert.Equal("mp3", requestJson.RootElement.GetProperty("response_format").GetString());
    }

    [Theory]
    [InlineData(24000, 1, false)]
    [InlineData(44100, 1, false)]
    [InlineData(48000, 2, false)]
    [InlineData(24000, 1, true)]
    [InlineData(44100, 1, true)]
    [InlineData(48000, 2, true)]
    public async Task GenerateAudio_PreservesEncodedFormatWithoutRateHeaders(
        int sampleRate, int channels, bool streaming)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            // A non-seekable HTTP body exercises the network buffering path too.
            Content = new StreamContent(new NonSeekableStream(await ReadToneAsync(sampleRate, channels)))
        };
        response.Content.Headers.ContentType = new("audio/mpeg");
        var handler = new RecordingHttpMessageHandler(response);
        var service = CreateService(handler);
        var request = new TtsRequest
        {
            Message = "Hello",
            ModelId = "future-provider/unknown-model",
            VoiceId = "voice"
        };

        await using var audio = streaming
            ? await service.GenerateAudioStreamAsync(request)
            : new MemoryStream(await service.GenerateAudioAsync(request));
        using var buffered = new MemoryStream();
        await audio.CopyToAsync(buffered);
        buffered.Position = 0;
        using var reader = new WaveFileReader(buffered);

        Assert.Equal(sampleRate, reader.WaveFormat.SampleRate);
        Assert.Equal(channels, reader.WaveFormat.Channels);
        Assert.Equal(16, reader.WaveFormat.BitsPerSample);
        Assert.Equal(WaveFormatEncoding.Pcm, reader.WaveFormat.Encoding);
        var samples = reader.ToSampleProvider();
        var buffer = new float[sampleRate * channels];
        var count = samples.Read(buffer, 0, buffer.Length);
        // Streaming WAV has an unknown length in its header. Measure actual decoded
        // samples instead; MP3 padding adds a little silence to the 200 ms fixture.
        Assert.InRange((double)count / (sampleRate * channels), 0.2, 0.3);
        Assert.Contains(buffer.Take(count), sample => Math.Abs(sample) > 0.01);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GenerateAudioStreamAsync_ProducesPcmBeforeUpstreamCompletes()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var mp3 = await ReadToneAsync(44100, 1);
        var source = new GatedStream(mp3);
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(source) };
        var service = CreateService(new RecordingHttpMessageHandler(response));
        await using (var audio = await service.GenerateAudioStreamAsync(new TtsRequest
        {
            Message = "Hello", ModelId = "future/model", VoiceId = "voice"
        }, timeout.Token))
        {
            var header = new byte[44];
            await audio.ReadExactlyAsync(header, timeout.Token);
            Assert.Equal("RIFF", Encoding.ASCII.GetString(header, 0, 4));
            Assert.Equal(44100, BitConverter.ToInt32(header, 24));
            var pcm = new byte[4096];
            Assert.True(await audio.ReadAsync(pcm, timeout.Token) > 0);
            Assert.True(source.Position < mp3.Length);

            var remainder = audio.CopyToAsync(Stream.Null, timeout.Token);
            await source.Blocked.Task.WaitAsync(timeout.Token);
            Assert.False(remainder.IsCompleted);
            source.Release.TrySetResult();
            await remainder;
        }
        Assert.True(source.WasDisposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GenerateAudioStreamAsync_CancelsWhileWaitingForNextFrame(bool cancelRequest)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var requestCancellation = new CancellationTokenSource();
        using var readCancellation = new CancellationTokenSource();
        var source = new GatedStream(await ReadToneAsync(44100, 1));
        var service = CreateService(new RecordingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(source)
        }));
        await using (var audio = await service.GenerateAudioStreamAsync(new TtsRequest
        {
            Message = "Hello", ModelId = "future/model", VoiceId = "voice"
        }, requestCancellation.Token))
        {
            var copying = audio.CopyToAsync(Stream.Null, readCancellation.Token);
            await source.Blocked.Task.WaitAsync(timeout.Token);
            await (cancelRequest ? requestCancellation : readCancellation).CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => copying.WaitAsync(timeout.Token));
        }
        Assert.True(source.WasDisposed);
    }

    [Fact]
    public async Task GenerateAudioStreamAsync_DisposeCancelsPendingRead()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var source = new GatedStream(await ReadToneAsync(44100, 1));
        var service = CreateService(new RecordingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(source)
        }));
        await using var audio = await service.GenerateAudioStreamAsync(new TtsRequest
        {
            Message = "Hello", ModelId = "future/model", VoiceId = "voice"
        }, timeout.Token);
        var copying = audio.CopyToAsync(Stream.Null, timeout.Token);
        await source.Blocked.Task.WaitAsync(timeout.Token);
        await audio.DisposeAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => copying.WaitAsync(timeout.Token));
        Assert.True(source.WasDisposed);
    }

    [Fact]
    public async Task GenerateAudioStreamAsync_TruncatedFrameThrowsAndDisposesResponse()
    {
        var mp3 = await ReadToneAsync(44100, 1);
        var source = new GatedStream(mp3[..^20]);
        source.Release.TrySetResult();
        var service = CreateService(new RecordingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(source)
        }));
        await using (var audio = await service.GenerateAudioStreamAsync(new TtsRequest
        {
            Message = "Hello", ModelId = "future/model", VoiceId = "voice"
        }))
        {
            await Assert.ThrowsAsync<EndOfStreamException>(() => audio.CopyToAsync(Stream.Null));
        }
        Assert.True(source.WasDisposed);
    }

    private sealed class GatedStream(byte[] bytes) : MemoryStream(bytes)
    {
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Blocked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool WasDisposed { get; private set; }
        public override bool CanSeek => false;

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var boundary = (int)Length / 2;
            if (Position >= boundary && !Release.Task.IsCompleted)
            {
                Blocked.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            // Fragment headers and frame bodies across network reads.
            var count = Math.Min(buffer.Length, 3);
            if (Position < boundary) count = Math.Min(count, boundary - (int)Position);
            return await base.ReadAsync(buffer[..count], cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }
    }

    private static Task<byte[]> ReadToneAsync(int sampleRate, int channels) =>
        File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Audio",
            $"tone-{sampleRate}-{(channels == 1 ? "mono" : "stereo")}.mp3"));

    private sealed class NonSeekableStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
    }

    private static OpenRouterTtsService CreateService(RecordingHttpMessageHandler handler, ILogger<OpenRouterTtsService>? logger = null)
    {
        return new OpenRouterTtsService(
            new HttpClient(handler),
            logger ?? NullLogger<OpenRouterTtsService>.Instance,
            new FakeIntegrationsService(new IntegrationsConfig
            {
                OpenRouterApiKey = "test-openrouter-key"
            }));
    }

    private sealed class RecordingLogger : ILogger<OpenRouterTtsService>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
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
