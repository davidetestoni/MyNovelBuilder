using System.Net;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Helpers;
using MyNovelBuilder.WebApi.Models.AudioGeneration;
using MyNovelBuilder.WebApi.Models.Integrations;
using MyNovelBuilder.WebApi.Models.Prompts;
using MyNovelBuilder.WebApi.Models.TextGeneration;
using MyNovelBuilder.WebApi.Models.Tts;
using MyNovelBuilder.WebApi.Options;
using MyNovelBuilder.WebApi.Services;
using MyNovelBuilder.WebApi.Services.TextGeneration;
using MyNovelBuilder.WebApi.Services.Tts;

namespace MyNovelBuilder.WebApi.Tests.Unit.Services;

public sealed class TtsFrozenInputsTests
{
    [Theory]
    [InlineData(TtsProvider.OmniVoice, false)]
    [InlineData(TtsProvider.OmniVoice, true)]
    [InlineData(TtsProvider.Qwen3, false)]
    [InlineData(TtsProvider.Qwen3, true)]
    [InlineData(TtsProvider.Chatterbox, false)]
    [InlineData(TtsProvider.Chatterbox, true)]
    [InlineData(TtsProvider.Audio8, false)]
    [InlineData(TtsProvider.Audio8, true)]
    public async Task EveryProviderChunkUsesValidatedEndpointAndRecording(TtsProvider provider, bool streamed)
    {
        using var fixture = new Fixture(provider);
        var revision = await fixture.Voices.GetRevisionAsync(provider, fixture.VoiceId);
        // Simulate edits after validation but before synthesis (also covers lazy stream producers).
        var revisions = new CapturingVoices(fixture.Voices, fixture.ChangeLiveInputs);
        var service = fixture.CreateService(revisions);
        var request = fixture.Request(revision);
        if (streamed)
        {
            await using var audio = await service.GenerateWavStreamAsync(request);
            await audio.CopyToAsync(Stream.Null);
        }
        else
        {
            Assert.NotEmpty(await service.GenerateWavBytesAsync(request));
        }

        Assert.True(fixture.Handler.Calls.Count > 1, "Exercise multiple provider chunks.");
        foreach (var call in fixture.Handler.Calls)
        {
            Assert.Equal("original.test", call.Host);
            Assert.Equal(Fixture.OriginalAudio, call.Audio);
            if (provider == TtsProvider.Audio8) Assert.Equal("original transcript", call.Transcript);
            else Assert.Equal(provider == TtsProvider.Qwen3 ? "italian" : "it", call.Language);
        }
    }

    [Fact]
    public async Task ChangesDuringEmphasisCannotChangeSynthesisInputs()
    {
        using var fixture = new Fixture(TtsProvider.OmniVoice);
        var revision = await fixture.Voices.GetRevisionAsync(TtsProvider.OmniVoice, fixture.VoiceId);
        var text = new CallbackTextService(fixture.ChangeLiveInputs);
        var service = fixture.CreateService(fixture.Voices, text);
        var request = fixture.Request(revision, emphasis: true);
        await service.GenerateWavBytesAsync(request);
        Assert.Equal(1, text.Calls);
        Assert.All(fixture.Handler.Calls, call =>
        {
            Assert.Equal("original.test", call.Host);
            Assert.Equal(Fixture.OriginalAudio, call.Audio);
            Assert.Equal("it", call.Language);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ChangedInputsBeforeExecutionFailBeforeAnyProviderCall(bool changeEndpoint)
    {
        using var fixture = new Fixture(TtsProvider.OmniVoice);
        var revision = await fixture.Voices.GetRevisionAsync(TtsProvider.OmniVoice, fixture.VoiceId);
        var request = fixture.Request(revision, emphasis: true);
        if (changeEndpoint) fixture.Config.OmniVoiceBaseUrl = "http://changed.test/";
        else File.WriteAllBytes(fixture.VoicePath, [99]);
        var text = new CallbackTextService(() => { });
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.CreateService(fixture.Voices, text).GenerateWavBytesAsync(request));
        Assert.Equal(0, text.Calls);
        Assert.Empty(fixture.Handler.Calls);
    }

    [Theory]
    [InlineData(TtsProvider.Chatterbox)]
    [InlineData(TtsProvider.Audio8)]
    public async Task BuiltInDefaultVoiceDoesNotRequireARecording(TtsProvider provider)
    {
        using var fixture = new Fixture(provider);
        Assert.Null(await fixture.Voices.GetRevisionAsync(provider, "default"));
    }

    private sealed class Fixture : IDisposable
    {
        public static readonly byte[] OriginalAudio = [1, 2, 3, 4];
        private readonly string _folder = Path.Combine(Path.GetTempPath(), $"audiobook-tests-{Guid.NewGuid()}");
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private readonly AppDbContext _database;
        private readonly ServiceProvider _providerServices;
        private readonly HttpClient _httpClient;
        private readonly TtsProvider _provider;
        private readonly ITtsService _tts;
        public string VoiceId { get; } = Guid.NewGuid().ToString();
        public string VoicePath => Path.Combine(_folder, "voices", $"{VoiceId}.wav");
        public IntegrationsConfig Config { get; } = new()
        {
            OmniVoiceBaseUrl = "http://original.test/", Qwen3BaseUrl = "http://original.test/",
            ChatterboxBaseUrl = "http://original.test/", Audio8BaseUrl = "http://original.test/"
        };
        public RecordingHandler Handler { get; } = new();
        public TtsVoiceRevisionService Voices { get; }

        public Fixture(TtsProvider provider)
        {
            _provider = provider;
            Directory.CreateDirectory(Path.GetDirectoryName(VoicePath)!);
            File.WriteAllBytes(VoicePath, OriginalAudio);
            _connection.Open();
            _database = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
            _database.Database.EnsureCreated();
            _database.Voices.Add(new Voice
            {
                Id = Guid.Parse(VoiceId), Name = "Voice", VoiceGender = VoiceGender.Both,
                Language = WritingLanguage.Italian, Transcript = "original transcript"
            });
            _database.SaveChanges();
            var storage = Microsoft.Extensions.Options.Options.Create(new AppStorageOptions { DataFolder = _folder });
            Voices = new TtsVoiceRevisionService(_database, storage);
            _providerServices = new ServiceCollection().AddSingleton(_database).BuildServiceProvider();
            var scopes = _providerServices.GetRequiredService<IServiceScopeFactory>();
            _httpClient = new HttpClient(Handler);
            var integrations = new Integrations(Config);
            _tts = provider switch
            {
                TtsProvider.OmniVoice => new OmniVoiceTtsService(_httpClient, storage, scopes, integrations),
                TtsProvider.Qwen3 => new Qwen3TtsService(_httpClient, storage, scopes, integrations),
                TtsProvider.Chatterbox => new ChatterboxTtsService(_httpClient, storage, scopes, integrations),
                TtsProvider.Audio8 => new Audio8TtsService(_httpClient, storage, scopes, integrations),
                _ => throw new ArgumentOutOfRangeException(nameof(provider))
            };
        }

        public void ChangeLiveInputs()
        {
            Config.OmniVoiceBaseUrl = Config.Qwen3BaseUrl = Config.ChatterboxBaseUrl = Config.Audio8BaseUrl = "http://changed.test/";
            File.WriteAllBytes(VoicePath, [99]);
            _database.Voices.ExecuteUpdate(update => update
                .SetProperty(voice => voice.Language, WritingLanguage.English)
                .SetProperty(voice => voice.Transcript, "changed transcript"));
        }

        public TextToSpeechGenerationRequest Request(string? revision, bool emphasis = false) => new()
        {
            Message = string.Join(" ", Enumerable.Repeat("This sentence makes several chunks.", 40)),
            ResolvedOptions = new(_provider, "model", VoiceId, TextGenerationProvider.OpenRouter, "text-model",
                emphasis, TtsEndpointIdentity.FromConfig(Config, _provider), revision)
        };

        public TtsAudioGenerationService CreateService(ITtsVoiceRevisionService revisions, CallbackTextService? text = null)
        {
            var services = new ServiceCollection().AddKeyedSingleton(_provider, _tts);
            services.AddKeyedSingleton<ITextGenerationService>(TextGenerationProvider.OpenRouter, text ?? new CallbackTextService(() => { }));
            return new TtsAudioGenerationService(services.BuildServiceProvider(), new Integrations(Config),
                new NoCache(), new NoDefaultTextResolver(), NullLogger<TtsAudioGenerationService>.Instance, revisions,
                new FileSystemAudioArtifactRepository(Microsoft.Extensions.Options.Options.Create(new AppStorageOptions { DataFolder = _folder })));
        }

        public void Dispose()
        {
            _httpClient.Dispose();
            _providerServices.Dispose();
            _database.Dispose();
            _connection.Dispose();
            Directory.Delete(_folder, recursive: true);
        }
    }

    private sealed class CapturingVoices(ITtsVoiceRevisionService inner, Action afterCapture) : ITtsVoiceRevisionService
    {
        public Task<string?> GetRevisionAsync(TtsProvider provider, string voiceId, CancellationToken cancellationToken = default)
            => inner.GetRevisionAsync(provider, voiceId, cancellationToken);
        public async Task<RecordedTtsVoice?> CaptureAsync(TtsProvider provider, string voiceId, CancellationToken cancellationToken = default)
        {
            var result = await inner.CaptureAsync(provider, voiceId, cancellationToken);
            afterCapture();
            return result;
        }
    }

    private sealed record RecordedCall(string Host, byte[]? Audio, string? Language, string? Transcript);
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<RecordedCall> Calls { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var parts = ((MultipartFormDataContent)request.Content!).ToDictionary(
                part => part.Headers.ContentDisposition!.Name!.Trim('"'));
            Calls.Add(new RecordedCall(request.RequestUri!.Host,
                parts.TryGetValue("reference_wav", out var audio) ? await audio.ReadAsByteArrayAsync(cancellationToken) : null,
                parts.TryGetValue("language", out var language) ? await language.ReadAsStringAsync(cancellationToken) : null,
                parts.TryGetValue("ref_text", out var transcript) ? await transcript.ReadAsStringAsync(cancellationToken) : null));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 0, 2, 0]) };
        }
    }

    private sealed class Integrations(IntegrationsConfig config) : IIntegrationsService
    {
        public ValueTask<IntegrationsConfig> GetConfigAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(config);
        public Task UpdateConfigAsync(IntegrationsConfig updated, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class NoCache : IAudioRepository
    {
        public Task<byte[]>? GetAudioFileAsync(AudioGenerationParameters parameters, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Frozen requests must not use legacy cache.");
        public Task SaveAudioFileAsync(AudioGenerationParameters parameters, byte[] audioData, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }
    private sealed class NoDefaultTextResolver : ITextGenerationServiceResolver
    {
        public ValueTask<ITextGenerationService> GetConfiguredServiceAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("Must use explicit provider.");
    }
    private sealed class CallbackTextService(Action callback) : ITextGenerationService
    {
        public int Calls { get; private set; }
        public Task<string> GenerateAsync(string modelId, IEnumerable<PromptMessage> messages, StructuredOutputOptions? structuredOutputOptions = null, CancellationToken cancellationToken = default)
        {
            Assert.Equal("text-model", modelId);
            Calls++;
            callback();
            return Task.FromResult(string.Join(" ", Enumerable.Repeat("Emphasized speech.", 100)));
        }
        public IAsyncEnumerable<string> GenerateStreamedAsync(string model, IEnumerable<PromptMessage> messages, StructuredOutputOptions? structuredOutputOptions = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> DescribeImageAsync(string model, IEnumerable<PromptMessage> messages, byte[] imageBytes, string imageMimeType, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<TextGenerationModelInfo>> GetAvailableModelsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
