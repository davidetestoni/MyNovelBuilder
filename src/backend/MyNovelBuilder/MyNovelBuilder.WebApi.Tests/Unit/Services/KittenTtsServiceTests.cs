using System.Net;
using MyNovelBuilder.WebApi.Exceptions;
using NAudio.Wave;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Models.Integrations;
using MyNovelBuilder.WebApi.Models.Tts;
using MyNovelBuilder.WebApi.Options;
using MyNovelBuilder.WebApi.Services;
using MyNovelBuilder.WebApi.Services.Tts;

namespace MyNovelBuilder.WebApi.Tests.Unit.Services;

public class KittenTtsServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GenerateAudio_WithCustomVoice_SendsReferenceAndTranscript(bool streaming)
    {
        await using var test = await KittenTtsTestContext.CreateAsync();
        var voice = await test.AddVoiceAsync("Clone", "The exact words in the sample.");
        await File.WriteAllBytesAsync(test.GetVoicePath(voice.Id), [0x52, 0x49, 0x46, 0x46]);

        var request = new TtsRequest
        {
            Message = "Hello from KittenTTS.",
            VoiceId = voice.Id.ToString()
        };
        byte[] audio;
        if (streaming)
        {
            await using var stream = await test.Service.GenerateAudioStreamAsync(request);
            using var output = new MemoryStream();
            await stream.CopyToAsync(output);
            audio = output.ToArray();
        }
        else
        {
            audio = await test.Service.GenerateAudioAsync(request);
        }

        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(audio, 0, 4));
        var sent = Assert.Single(test.Handler.Requests);
        Assert.Equal("http://kitten.test/api/tts", sent.RequestUri?.ToString());
        var body = await sent.Content!.ReadAsStringAsync();
        Assert.Contains("name=text", body);
        Assert.Contains("Hello from KittenTTS.", body);
        Assert.Contains("name=ref_text", body);
        Assert.Contains("The exact words in the sample.", body);
        Assert.Contains("name=reference_wav", body);
        Assert.DoesNotContain("name=voice", body);
        Assert.Contains("name=output_format", body);
    }

    [Fact]
    public async Task GetModelsAsync_OffersServerVoicesAndOnlyTranscriptReadyVoices()
    {
        await using var test = await KittenTtsTestContext.CreateAsync();
        var readyVoice = await test.AddVoiceAsync("Ready", "Spoken sample");
        _ = await test.AddVoiceAsync("Missing transcript", null);
        _ = await test.AddVoiceAsync("Blank transcript", "   ");

        var model = Assert.Single(await test.Service.GetModelsAsync());

        Assert.Equal("KittenML/kitten-tts-2", model.ModelId);
        Assert.Equal("http://kitten.test/api/model", Assert.Single(test.Handler.Requests).RequestUri?.ToString());
        Assert.Collection(
            model.Voices,
            voice => Assert.Equal("Bruno", voice.VoiceId),
            voice => Assert.Equal("Luna", voice.VoiceId),
            voice => Assert.Equal(readyVoice.Id, Guid.Parse(voice.VoiceId)));
    }

    [Fact]
    public async Task GenerateAudioAsync_WithBuiltInVoice_UsesServerSampleRateAndSendsNoReference()
    {
        await using var test = await KittenTtsTestContext.CreateAsync();
        test.Handler.SampleRate = "22050";
        var audio = await test.Service.GenerateAudioAsync(new TtsRequest
        {
            Message = "Hello.", VoiceId = "Bruno"
        });

        using var reader = new WaveFileReader(new MemoryStream(audio));
        Assert.Equal(22050, reader.WaveFormat.SampleRate);
        Assert.Equal(1, reader.WaveFormat.Channels);
        Assert.Equal(16, reader.WaveFormat.BitsPerSample);
        Assert.Equal(4, reader.Length);
        var body = await Assert.Single(test.Handler.Requests).Content!.ReadAsStringAsync();
        Assert.Contains("name=voice", body);
        Assert.Contains("Bruno", body);
        Assert.DoesNotContain("reference_wav", body);
        Assert.DoesNotContain("ref_text", body);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GenerateAudio_WithLongText_CombinesPcmChunksInOneWav(bool streaming)
    {
        await using var test = await KittenTtsTestContext.CreateAsync();
        test.Handler.SampleRate = "22050";
        var request = new TtsRequest
        {
            Message = string.Join(" ", Enumerable.Repeat("This is a sentence.", 80)), VoiceId = "Luna"
        };
        byte[] audio;
        if (streaming)
        {
            await using var stream = await test.Service.GenerateAudioStreamAsync(request);
            using var output = new MemoryStream();
            await stream.CopyToAsync(output);
            audio = output.ToArray();
            Assert.Equal(44 + test.Handler.Requests.Count * 4, audio.Length);
        }
        else
        {
            audio = await test.Service.GenerateAudioAsync(request);
            using var reader = new WaveFileReader(new MemoryStream(audio));
            Assert.Equal(test.Handler.Requests.Count * 4, reader.Length);
        }

        Assert.True(test.Handler.Requests.Count > 1);
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(audio, 0, 4));
        Assert.Equal(22050, BitConverter.ToInt32(audio, 24));
        foreach (var sent in test.Handler.Requests)
        {
            var body = await sent.Content!.ReadAsStringAsync();
            Assert.Contains("Luna", body);
            Assert.Contains("name=output_format", body);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task GenerateAudioAsync_WithMissingTranscript_FailsBeforeHttpRequest(string? transcript)
    {
        await using var test = await KittenTtsTestContext.CreateAsync();
        var voice = await test.AddVoiceAsync("Clone", transcript);
        await Assert.ThrowsAsync<ApiException>(() => test.Service.GenerateAudioAsync(new TtsRequest
        {
            Message = "Hello.", VoiceId = voice.Id.ToString()
        }));
        Assert.Empty(test.Handler.Requests);
    }

    [Fact]
    public async Task GenerateAudioAsync_WithMissingSample_FailsBeforeHttpRequest()
    {
        await using var test = await KittenTtsTestContext.CreateAsync();
        var voice = await test.AddVoiceAsync("Clone", "Sample words.");
        await Assert.ThrowsAsync<ApiException>(() => test.Service.GenerateAudioAsync(new TtsRequest
        {
            Message = "Hello.", VoiceId = voice.Id.ToString()
        }));
        Assert.Empty(test.Handler.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GenerateAudio_WhenServerFails_ThrowsHttpError(bool streaming)
    {
        await using var test = await KittenTtsTestContext.CreateAsync();
        test.Handler.StatusCode = HttpStatusCode.BadRequest;
        var request = new TtsRequest { Message = "Hello.", VoiceId = "Bruno" };
        if (streaming)
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => test.Service.GenerateAudioStreamAsync(request));
        }
        else
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => test.Service.GenerateAudioAsync(request));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("invalid")]
    public async Task GenerateAudioAsync_WithoutValidSampleRate_Throws(string? sampleRate)
    {
        await using var test = await KittenTtsTestContext.CreateAsync();
        test.Handler.SampleRate = sampleRate;
        await Assert.ThrowsAsync<InvalidOperationException>(() => test.Service.GenerateAudioAsync(new TtsRequest
        {
            Message = "Hello.", VoiceId = "Bruno"
        }));
    }

    private sealed class KittenTtsTestContext : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _serviceProvider;
        private readonly string _dataFolder;

        public KittenTtsService Service { get; }
        public RecordingHttpMessageHandler Handler { get; }

        private KittenTtsTestContext(
            SqliteConnection connection,
            ServiceProvider serviceProvider,
            string dataFolder,
            KittenTtsService service,
            RecordingHttpMessageHandler handler)
        {
            _connection = connection;
            _serviceProvider = serviceProvider;
            _dataFolder = dataFolder;
            Service = service;
            Handler = handler;
        }

        public static async Task<KittenTtsTestContext> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var services = new ServiceCollection();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
            var serviceProvider = services.BuildServiceProvider();

            await using (var scope = serviceProvider.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
            }

            var dataFolder = Path.Combine(Path.GetTempPath(), $"mnb-kitten-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path.Combine(dataFolder, "voices"));
            var handler = new RecordingHttpMessageHandler();
            var service = new KittenTtsService(
                new HttpClient(handler),
                Microsoft.Extensions.Options.Options.Create(
                    new AppStorageOptions { DataFolder = dataFolder }),
                serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                new FakeIntegrationsService());

            return new KittenTtsTestContext(
                connection,
                serviceProvider,
                dataFolder,
                service,
                handler);
        }

        public async Task<Voice> AddVoiceAsync(string name, string? transcript)
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var voice = new Voice
            {
                Name = name,
                VoiceGender = VoiceGender.Both,
                Language = WritingLanguage.English,
                Transcript = transcript
            };
            dbContext.Voices.Add(voice);
            await dbContext.SaveChangesAsync();
            return voice;
        }

        public string GetVoicePath(Guid id) =>
            Path.Combine(_dataFolder, "voices", $"{id}.wav");

        public async ValueTask DisposeAsync()
        {
            await _serviceProvider.DisposeAsync();
            await _connection.DisposeAsync();
            Directory.Delete(_dataFolder, recursive: true);
        }
    }

    private sealed class FakeIntegrationsService : IIntegrationsService
    {
        public ValueTask<IntegrationsConfig> GetConfigAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new IntegrationsConfig
            {
                KittenTtsBaseUrl = "http://kitten.test/api"
            });

        public Task UpdateConfigAsync(
            IntegrationsConfig config,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    public sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
        public string? SampleRate { get; set; } = "24000";

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(await CloneRequestAsync(request, cancellationToken));
            if (request.Method == HttpMethod.Get)
            {
                return new HttpResponseMessage(StatusCode)
                {
                    Content = new StringContent("""
                        {"checkpoint":"KittenML/kitten-tts-2","available_voices":["Bruno","Luna"]}
                        """)
                };
            }
            var response = new HttpResponseMessage(StatusCode)
            {
                Content = new ByteArrayContent([0x00, 0x00, 0x01, 0x00])
            };
            if (SampleRate is not null)
            {
                response.Headers.Add("X-Sample-Rate", SampleRate);
            }
            return response;
        }

        private static async Task<HttpRequestMessage> CloneRequestAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri);

            if (request.Content is null)
            {
                return clone;
            }

            var contentBytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            clone.Content = new ByteArrayContent(contentBytes);

            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return clone;
        }
    }
}
