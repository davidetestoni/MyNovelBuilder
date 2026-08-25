using System.Net;
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

public class Audio8TtsServiceTests
{
    [Fact]
    public async Task GenerateAudioAsync_WithCustomVoice_SendsReferenceAndTranscript()
    {
        await using var test = await Audio8TestContext.CreateAsync();
        var voice = await test.AddVoiceAsync("Clone", "The exact words in the sample.");
        await File.WriteAllBytesAsync(test.GetVoicePath(voice.Id), [0x52, 0x49, 0x46, 0x46]);

        var audio = await test.Service.GenerateAudioAsync(new TtsRequest
        {
            Message = "Hello from Audio8.",
            VoiceId = voice.Id.ToString()
        });

        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(audio, 0, 4));
        var request = Assert.Single(test.Handler.Requests);
        Assert.Equal("http://audio8.test/tts", request.RequestUri?.ToString());
        var body = await request.Content!.ReadAsStringAsync();
        Assert.Contains("name=text", body);
        Assert.Contains("Hello from Audio8.", body);
        Assert.Contains("name=ref_text", body);
        Assert.Contains("The exact words in the sample.", body);
        Assert.Contains("name=reference_wav", body);
    }

    [Fact]
    public async Task GetModelsAsync_OffersDefaultAndOnlyTranscriptReadyVoices()
    {
        await using var test = await Audio8TestContext.CreateAsync();
        var readyVoice = await test.AddVoiceAsync("Ready", "Spoken sample");
        _ = await test.AddVoiceAsync("Missing transcript", null);
        _ = await test.AddVoiceAsync("Blank transcript", "   ");

        var model = Assert.Single(await test.Service.GetModelsAsync());

        Assert.Equal("Audio8-TTS-Preview-0.6b", model.ModelId);
        Assert.Collection(
            model.Voices,
            voice => Assert.Equal("default", voice.VoiceId),
            voice => Assert.Equal(readyVoice.Id, Guid.Parse(voice.VoiceId)));
    }

    private sealed class Audio8TestContext : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _serviceProvider;
        private readonly string _dataFolder;

        public Audio8TtsService Service { get; }
        public RecordingHttpMessageHandler Handler { get; }

        private Audio8TestContext(
            SqliteConnection connection,
            ServiceProvider serviceProvider,
            string dataFolder,
            Audio8TtsService service,
            RecordingHttpMessageHandler handler)
        {
            _connection = connection;
            _serviceProvider = serviceProvider;
            _dataFolder = dataFolder;
            Service = service;
            Handler = handler;
        }

        public static async Task<Audio8TestContext> CreateAsync()
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

            var dataFolder = Path.Combine(Path.GetTempPath(), $"mnb-audio8-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path.Combine(dataFolder, "voices"));
            var handler = new RecordingHttpMessageHandler();
            var service = new Audio8TtsService(
                new HttpClient(handler),
                Microsoft.Extensions.Options.Options.Create(
                    new AppStorageOptions { DataFolder = dataFolder }),
                serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                new FakeIntegrationsService());

            return new Audio8TestContext(
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
                Audio8BaseUrl = "http://audio8.test/"
            });

        public Task UpdateConfigAsync(
            IntegrationsConfig config,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    public sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(await CloneRequestAsync(request, cancellationToken));
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([0x00, 0x00, 0x01, 0x00])
            };
            response.Headers.Add("X-Sample-Rate", "44100");
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
