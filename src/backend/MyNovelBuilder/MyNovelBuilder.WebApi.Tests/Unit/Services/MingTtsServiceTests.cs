using System.Net;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Models.Integrations;
using MyNovelBuilder.WebApi.Models.Tts;
using MyNovelBuilder.WebApi.Options;
using MyNovelBuilder.WebApi.Services;
using MyNovelBuilder.WebApi.Services.Tts;
using NAudio.Wave;

namespace MyNovelBuilder.WebApi.Tests.Unit.Services;

public class MingTtsServiceTests
{
    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(false, "   ")]
    [InlineData(true, "   ")]
    [InlineData(false, "  Exact reference words.  ")]
    [InlineData(true, "  Exact reference words.  ")]
    public async Task Clone_WithOrWithoutTranscript_SendsReferenceAndProducesWav(bool streamed, string? transcript)
    {
        await using var test = await Context.CreateAsync();
        var voice = await test.AddVoiceAsync("Clone", WritingLanguage.German, transcript);
        var audio = await GenerateAsync(test.Service, new TtsRequest
        {
            Message = "Hallo Welt.", VoiceId = voice.Id.ToString()
        }, streamed);

        var call = Assert.Single(test.Handler.Calls);
        Assert.Equal("http://ming.test/api/tts", call.Uri.ToString());
        Assert.Equal("Hallo Welt.", call.Fields["text"]);
        Assert.Equal("de", call.Fields["language"]);
        Assert.Equal(Context.ReferenceAudio, call.Audio);
        Assert.Equal("audio/wav", call.ReferenceContentType);
        Assert.False(call.Fields.ContainsKey("speaker"));
        Assert.False(call.Fields.ContainsKey("instruct"));
        Assert.Equal(!string.IsNullOrWhiteSpace(transcript), call.Fields.ContainsKey("ref_text"));
        if (!string.IsNullOrWhiteSpace(transcript)) Assert.Equal(transcript.Trim(), call.Fields["ref_text"]);
        AssertWav(audio, 44100, Handler.Pcm);
    }

    [Theory]
    [InlineData("US_Emma")]
    [InlineData("UK_George")]
    [InlineData("UK_Charlotte")]
    public async Task BuiltInSpeaker_UsesSpeakerFieldAndResponseSampleRate(string speaker)
    {
        await using var test = await Context.CreateAsync();
        test.Handler.SampleRate = _ => "22050";
        var audio = await test.Service.GenerateAudioAsync(new TtsRequest { Message = "Hello.", VoiceId = speaker });

        var call = Assert.Single(test.Handler.Calls);
        Assert.Equal(speaker, call.Fields["speaker"]);
        Assert.Equal("en", call.Fields["language"]);
        Assert.Null(call.Audio);
        Assert.False(call.Fields.ContainsKey("ref_text"));
        AssertWav(audio, 22050, Handler.Pcm);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LongText_CombinesEveryPcmChunkIntoOneWav(bool streamed)
    {
        await using var test = await Context.CreateAsync();
        var audio = await GenerateAsync(test.Service, new TtsRequest
        {
            Message = string.Join(" ", Enumerable.Repeat("This is one sentence.", 80)), VoiceId = "US_Emma"
        }, streamed);

        Assert.True(test.Handler.Calls.Count > 1);
        Assert.All(test.Handler.Calls, call => Assert.InRange(call.Fields["text"].Length, 1, 500));
        AssertWav(audio, 44100, Enumerable.Range(0, test.Handler.Calls.Count).SelectMany(_ => Handler.Pcm).ToArray());
    }

    [Fact]
    public async Task Models_UseCheckpointAndIncludeSupportedVoicesWithoutRequiringTranscripts()
    {
        await using var test = await Context.CreateAsync();
        var english = await test.AddVoiceAsync("A", WritingLanguage.English, null);
        var french = await test.AddVoiceAsync("B", WritingLanguage.French, "   ");
        var spanish = await test.AddVoiceAsync("C", WritingLanguage.Spanish, "Sample");
        var german = await test.AddVoiceAsync("D", WritingLanguage.German, null);
        await test.AddVoiceAsync("Italian", WritingLanguage.Italian, "Sample");
        await test.AddVoiceAsync("Russian", WritingLanguage.Russian, "Sample");

        var model = Assert.Single(await test.Service.GetModelsAsync());

        Assert.Equal("http://ming.test/api/model", Assert.Single(test.Handler.Calls).Uri.ToString());
        Assert.Equal("inclusionAI/Ming-3.0-tts-0.5B", model.ModelId);
        Assert.Equal(new[] { "US_Emma", "UK_George", "UK_Charlotte" }, model.Voices.Take(3).Select(v => v.VoiceId));
        Assert.Equal(new[] { english.Id, french.Id, spanish.Id, german.Id },
            model.Voices.Skip(3).Select(v => Guid.Parse(v.VoiceId)));
    }

    [Fact]
    public async Task Models_WhenCloningDisabled_OffersNoSynthesisVoices()
    {
        await using var test = await Context.CreateAsync();
        test.Handler.CloneEnabled = false;
        await test.AddVoiceAsync("Clone", WritingLanguage.English, null);
        Assert.Empty(Assert.Single(await test.Service.GetModelsAsync()).Voices);
    }

    [Theory]
    [InlineData(WritingLanguage.English, "en")]
    [InlineData(WritingLanguage.French, "fr")]
    [InlineData(WritingLanguage.Spanish, "es")]
    [InlineData(WritingLanguage.German, "de")]
    public async Task Design_SendsDescriptionAndReturnsServerWav(WritingLanguage language, string code)
    {
        await using var test = await Context.CreateAsync();
        Assert.True(test.Service.SupportsVoiceDesign());
        var audio = await test.Service.VoiceDesignAsync("Words to speak.", language, "  A warm voice.  ");
        var call = Assert.Single(test.Handler.Calls);
        Assert.Equal("http://ming.test/api/voice-design", call.Uri.ToString());
        Assert.Equal("Words to speak.", call.Fields["prompt"]);
        Assert.Equal(code, call.Fields["language"]);
        Assert.Equal("A warm voice.", call.Fields["voice_description"]);
        Assert.Null(call.Audio);
        AssertWav(audio, 44100, Handler.Pcm);
    }

    [Theory]
    [InlineData(WritingLanguage.Italian)]
    [InlineData(WritingLanguage.Russian)]
    public async Task UnsupportedLanguage_FailsBeforeSynthesisOrDesign(WritingLanguage language)
    {
        await using var test = await Context.CreateAsync();
        var voice = await test.AddVoiceAsync("Unsupported", language, null);
        var error = await Assert.ThrowsAsync<ApiException>(() => test.Service.GenerateAudioAsync(
            new TtsRequest { Message = "Hello.", VoiceId = voice.Id.ToString() }));
        Assert.Contains(language.ToString(), error.Message);
        await Assert.ThrowsAsync<ApiException>(() => test.Service.VoiceDesignAsync("Hello.", language, "Warm voice."));
        Assert.Empty(test.Handler.Calls);
    }

    [Fact]
    public async Task MissingSample_FailsBeforeSendingRequest()
    {
        await using var test = await Context.CreateAsync();
        var voice = await test.AddVoiceAsync("Missing", WritingLanguage.English, null, writeSample: false);
        await Assert.ThrowsAsync<ApiException>(() => test.Service.GenerateAudioAsync(
            new TtsRequest { Message = "Hello.", VoiceId = voice.Id.ToString() }));
        Assert.Empty(test.Handler.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("default")]
    [InlineData("../sample.wav")]
    [InlineData("c3867c02-940a-4de4-a513-4ab119a6717f")]
    public async Task UnknownVoice_FailsBeforeSendingRequest(string? voiceId)
    {
        await using var test = await Context.CreateAsync();
        await Assert.ThrowsAsync<ApiException>(() => test.Service.GenerateAudioAsync(
            new TtsRequest { Message = "Hello.", VoiceId = voiceId! }));
        Assert.Empty(test.Handler.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("invalid")]
    public async Task InvalidSampleRate_IsRejected(string? rate)
    {
        await using var test = await Context.CreateAsync();
        test.Handler.SampleRate = _ => rate;
        await Assert.ThrowsAsync<InvalidOperationException>(() => test.Service.GenerateAudioAsync(
            new TtsRequest { Message = "Hello.", VoiceId = "US_Emma" }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SampleRateChangingBetweenChunks_IsRejected(bool streamed)
    {
        await using var test = await Context.CreateAsync();
        test.Handler.SampleRate = index => index == 1 ? "44100" : "24000";
        await Assert.ThrowsAsync<InvalidOperationException>(() => GenerateAsync(test.Service, new TtsRequest
        {
            Message = string.Join(" ", Enumerable.Repeat("This is a sentence.", 80)), VoiceId = "US_Emma"
        }, streamed));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HttpFailure_IsPropagated(bool streamed)
    {
        await using var test = await Context.CreateAsync();
        test.Handler.Status = HttpStatusCode.ServiceUnavailable;
        await Assert.ThrowsAsync<HttpRequestException>(() => GenerateAsync(test.Service,
            new TtsRequest { Message = "Hello.", VoiceId = "US_Emma" }, streamed));
    }

    [Fact]
    public async Task CancelledRequest_DoesNotSendAudio()
    {
        await using var test = await Context.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => test.Service.GenerateAudioAsync(
            new TtsRequest { Message = "Hello.", VoiceId = "US_Emma" }, cancellation.Token));
        Assert.Empty(test.Handler.Calls);
    }

    private static async Task<byte[]> GenerateAsync(MingTtsService service, TtsRequest request, bool streamed)
    {
        if (!streamed) return await service.GenerateAudioAsync(request);
        await using var stream = await service.GenerateAudioStreamAsync(request);
        using var output = new MemoryStream();
        await stream.CopyToAsync(output);
        return output.ToArray();
    }

    private static void AssertWav(byte[] audio, int rate, byte[] expectedPcm)
    {
        using var reader = new WaveFileReader(new MemoryStream(audio));
        Assert.Equal(rate, reader.WaveFormat.SampleRate);
        Assert.Equal(1, reader.WaveFormat.Channels);
        Assert.Equal(16, reader.WaveFormat.BitsPerSample);
        using var pcm = new MemoryStream();
        reader.CopyTo(pcm);
        Assert.Equal(expectedPcm, pcm.ToArray());
    }

    private sealed class Context : IAsyncDisposable
    {
        public static readonly byte[] ReferenceAudio = [0x52, 0x49, 0x46, 0x46];
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private readonly string _folder = Path.Combine(Path.GetTempPath(), $"mnb-ming-tests-{Guid.NewGuid():N}");
        private ServiceProvider _services = null!;
        private HttpClient _httpClient = null!;
        public MingTtsService Service { get; private set; } = null!;
        public Handler Handler { get; } = new();

        public static async Task<Context> CreateAsync()
        {
            var test = new Context();
            await test._connection.OpenAsync();
            test._services = new ServiceCollection().AddDbContext<AppDbContext>(
                options => options.UseSqlite(test._connection)).BuildServiceProvider();
            await using var scope = test._services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
            Directory.CreateDirectory(Path.Combine(test._folder, "voices"));
            test._httpClient = new HttpClient(test.Handler);
            test.Service = new MingTtsService(test._httpClient,
                Microsoft.Extensions.Options.Options.Create(new AppStorageOptions { DataFolder = test._folder }),
                test._services.GetRequiredService<IServiceScopeFactory>(), new Integrations());
            return test;
        }

        public async Task<Voice> AddVoiceAsync(string name, WritingLanguage language, string? transcript, bool writeSample = true)
        {
            await using var scope = _services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var voice = new Voice { Name = name, Language = language, Transcript = transcript, VoiceGender = VoiceGender.Both };
            database.Voices.Add(voice);
            await database.SaveChangesAsync();
            if (writeSample) await File.WriteAllBytesAsync(Path.Combine(_folder, "voices", $"{voice.Id}.wav"), ReferenceAudio);
            return voice;
        }

        public async ValueTask DisposeAsync()
        {
            _httpClient.Dispose();
            await _services.DisposeAsync();
            await _connection.DisposeAsync();
            Directory.Delete(_folder, recursive: true);
        }
    }

    private sealed class Integrations : IIntegrationsService
    {
        public ValueTask<IntegrationsConfig> GetConfigAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new IntegrationsConfig { MingBaseUrl = "http://ming.test/api" });
        public Task UpdateConfigAsync(IntegrationsConfig config, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed record Call(Uri Uri, Dictionary<string, string> Fields, byte[]? Audio, string? ReferenceContentType);
    private sealed class Handler : HttpMessageHandler
    {
        public static readonly byte[] Pcm = [0, 0, 1, 0];
        public List<Call> Calls { get; } = [];
        public Func<int, string?> SampleRate { get; set; } = _ => "44100";
        public bool CloneEnabled { get; set; } = true;
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fields = new Dictionary<string, string>();
            byte[]? audio = null;
            string? referenceContentType = null;
            if (request.Content is MultipartFormDataContent form)
            {
                foreach (var part in form)
                {
                    var key = part.Headers.ContentDisposition!.Name!.Trim('"');
                    if (key == "reference_wav")
                    {
                        audio = await part.ReadAsByteArrayAsync(cancellationToken);
                        referenceContentType = part.Headers.ContentType?.MediaType;
                    }
                    else fields.Add(key, await part.ReadAsStringAsync(cancellationToken));
                }
            }
            Calls.Add(new Call(request.RequestUri!, fields, audio, referenceContentType));
            var response = new HttpResponseMessage(Status);
            if (request.Method == HttpMethod.Get)
            {
                response.Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new
                {
                    checkpoint = "inclusionAI/Ming-3.0-tts-0.5B", voice_clone_enabled = CloneEnabled,
                    supported_speakers = new[] { "US_Emma", "UK_George", "UK_Charlotte", "灵小柔", "灵楷" }
                }));
            }
            else if (request.RequestUri!.AbsolutePath.EndsWith("voice-design"))
            {
                using var output = new MemoryStream();
                using (var writer = new WaveFileWriter(output, new WaveFormat(44100, 16, 1))) await writer.WriteAsync(Pcm);
                response.Content = new ByteArrayContent(output.ToArray());
            }
            else
            {
                response.Content = new ByteArrayContent(Pcm);
                if (SampleRate(Calls.Count) is { } rate) response.Headers.Add("X-Sample-Rate", rate);
            }
            return response;
        }
    }
}
