using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Models.AudioGeneration;
using MyNovelBuilder.WebApi.Models.Integrations;
using MyNovelBuilder.WebApi.Models.Prompts;
using MyNovelBuilder.WebApi.Models.Tts;
using MyNovelBuilder.WebApi.Options;
using MyNovelBuilder.WebApi.Services;
using MyNovelBuilder.WebApi.Services.TextGeneration;
using MyNovelBuilder.WebApi.Services.Tts;
using NAudio.Wave;

namespace MyNovelBuilder.WebApi.Tests.Unit.Services;

public sealed class AudioArtifactCacheTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "audio-cache-tests-" + Guid.NewGuid());
    private FileSystemAudioArtifactRepository Repository() => new(Microsoft.Extensions.Options.Options.Create(new AppStorageOptions { DataFolder = _folder }));
    private static readonly ResolvedTtsGenerationOptions Settings = new(TtsProvider.ElevenLabs, "model", "voice",
        TextGenerationProvider.OpenRouter, "text-model", true, null, null);
    private const string Story = "Private story text that must never appear in cache metadata.";
    private static byte[] Wav()
    {
        using var stream = new MemoryStream();
        using (var writer = new WaveFileWriter(stream, new WaveFormat(24000, 16, 1))) writer.Write(new byte[480], 0, 480);
        return stream.ToArray();
    }

    private TtsAudioGenerationService Service(FakeTts tts, IAudioArtifactRepository? repository = null)
    {
        var services = new ServiceCollection().AddKeyedSingleton<ITtsService>(TtsProvider.ElevenLabs, tts).BuildServiceProvider();
        return new(services, new Integrations(), new Legacy(), new Resolver(),
            NullLogger<TtsAudioGenerationService>.Instance, new NoRecordedVoices(), repository ?? Repository());
    }
    private static TextToSpeechGenerationRequest Request(ResolvedTtsGenerationOptions? options = null) =>
        new() { Message = Story, ResolvedOptions = options ?? Settings };

    [Fact]
    public async Task ConcurrentRequestsAndRestartReuseCompletedAudioWithoutPaidCallsOrTextOnDisk()
    {
        var tts = new FakeTts();
        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Service(tts).GenerateWavBytesAsync(Request())));
        Assert.All(results, bytes => Assert.Equal(Wav(), bytes));
        Assert.Equal(1, tts.SynthesisCalls);
        Assert.Equal(1, tts.EmphasisCalls);
        await using var stream = await Service(tts).GenerateWavStreamAsync(Request());
        Assert.IsType<FileStream>(stream);
        Assert.Equal(Wav().Length, stream.Length);
        Assert.Equal(1, tts.SynthesisCalls);
        Assert.Equal(1, tts.EmphasisCalls);
        foreach (var file in Directory.GetFiles(_folder, "*.json", SearchOption.AllDirectories))
        {
            var metadata = await File.ReadAllTextAsync(file);
            Assert.DoesNotContain(Story, metadata);
            Assert.DoesNotContain("Emphasized", metadata);
            Assert.DoesNotContain("voice", metadata);
        }
        Assert.Empty(Directory.GetFiles(_folder, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task CorruptOrTruncatedAudioRegeneratesAndUnpublishedFilesAreNeverHits()
    {
        var tts = new FakeTts();
        await Service(tts).GenerateWavBytesAsync(Request());
        var file = Assert.Single(Directory.GetFiles(_folder, "*.wav", SearchOption.AllDirectories));
        var bytes = await File.ReadAllBytesAsync(file);
        bytes[^1] ^= 1; // Valid WAV structure, invalid checksum.
        await File.WriteAllBytesAsync(file, bytes);
        await Service(tts).GenerateWavBytesAsync(Request());
        Assert.Equal(2, tts.SynthesisCalls);
        await File.WriteAllBytesAsync(file, bytes[..40]);
        await Service(tts).GenerateWavBytesAsync(Request());
        Assert.Equal(3, tts.SynthesisCalls);
        File.Delete(Path.ChangeExtension(file, ".json"));
        Assert.Null(await Repository().OpenReadAsync(Path.GetFileNameWithoutExtension(file)));
    }

    [Fact]
    public async Task InvalidOutputAndCancellationDoNotPublishAudioOrManifest()
    {
        var repository = Repository();
        var key = AudioCacheKey.Hash("test");
        await Assert.ThrowsAsync<InvalidDataException>(() => repository.SaveAsync(key, new MemoryStream(Wav()[..46])));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.SaveAsync(key, new MemoryStream(Wav()), cancelled.Token));
        Assert.Null(await repository.OpenReadAsync(key));
        Assert.Empty(Directory.GetFiles(_folder, "*.tmp", SearchOption.AllDirectories));
        await Assert.ThrowsAsync<InvalidDataException>(() => repository.SaveManifestAsync(new(1, key, null, [key])));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.OpenReadAsync("../../escape"));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.SaveManifestAsync(new(1, key, Story, [key])));
    }

    [Fact]
    public async Task InactiveAuxiliarySettingsDoNotInvalidateAndPromptChangesDo()
    {
        var tts = new FakeTts();
        var options = Settings with { EnableTextEmphasis = false };
        await Service(tts).GenerateWavBytesAsync(Request(options));
        await Service(tts).GenerateWavBytesAsync(Request(options with { TextGenerationModelId = "other" }));
        Assert.Equal(1, tts.SynthesisCalls);
        Assert.Equal(0, tts.EmphasisCalls);
        await Service(tts).GenerateWavBytesAsync(Request());
        tts.Prompt = "Changed prompt";
        await Service(tts).GenerateWavBytesAsync(Request());
        Assert.Equal(2, tts.EmphasisCalls);
        // The fake emits the same emphasized text: reuse synthesis despite changed preparation.
        Assert.Equal(2, tts.SynthesisCalls);
    }

    [Fact]
    public void FingerprintsCanonicalizeMapsPreserveOrderAndSeparateDependencies()
    {
        Assert.Equal(AudioCacheKey.Create("x", new { a = 1, b = 2 }), AudioCacheKey.Create("x", new { b = 2, a = 1 }));
        Assert.NotEqual(AudioCacheKey.Create("x", new[] { "a", "b" }), AudioCacheKey.Create("x", new[] { "b", "a" }));
        Assert.NotEqual(AudioCacheKey.Create("x", new { a = (string?)null }), AudioCacheKey.Create("x", new { }));
        var key = AudioCacheKey.Synthesis(Story, Settings);
        Assert.Equal(key, AudioCacheKey.Synthesis(Story, Settings with { EnableTextEmphasis = false, TextGenerationModelId = "other" }));
        Assert.NotEqual(key, AudioCacheKey.Synthesis(Story, Settings with { VoiceRevision = "changed" }));
        Assert.NotEqual(key, AudioCacheKey.Synthesis(Story, Settings with { EndpointIdentity = "changed" }));
        Assert.NotEqual(key, AudioCacheKey.Synthesis(Story, Settings with { ModelId = "changed" }));
        Assert.NotEqual(AudioCacheKey.Section([key], 0), AudioCacheKey.Section([key], 150));
    }

    [Fact]
    public void ImmersiveFingerprintsSeparatePlanningVoicesAndAssembly()
    {
        var character = Guid.NewGuid();
        var section = new AudiobookSectionSnapshot(0, Story,
            [new(PromptMessageRole.User, "Context and story")],
            [new(character, "character-voice", DateTime.UtcNow, "revision")]);
        var settings = new ResolvedAudiobookSettings(Settings.Provider, Settings.ModelId, Settings.VoiceId,
            false, true, 150, Settings.TextGenerationProvider, Settings.TextGenerationModelId, Guid.NewGuid(), null, null);
        var schema = new MyNovelBuilder.WebApi.Models.TextGeneration.StructuredOutputOptions { SchemaName = "plan", JsonSchema = "{}" };
        var preparation = AudioCacheKey.ImmersivePreparation(section, settings, schema);
        var source = AudioCacheKey.ImmersiveSource(section, settings, preparation, null);
        Assert.Equal(source, AudioCacheKey.ImmersiveSource(section with { SectionIndex = 5 }, settings with { ImmersivePauseMs = 0 }, preparation, null));
        Assert.NotEqual(preparation, AudioCacheKey.ImmersivePreparation(section with
        {
            ImmersivePrompt = [new(PromptMessageRole.User, "Changed context")]
        }, settings, schema));
        Assert.NotEqual(source, AudioCacheKey.ImmersiveSource(section with
        {
            VoiceAssignments = [section.VoiceAssignments[0] with { VoiceRevision = "new-revision" }]
        }, settings, preparation, null));
        Assert.Equal(source, AudioCacheKey.ImmersiveSource(section with
        {
            VoiceAssignments = [section.VoiceAssignments[0] with { UpdatedAt = DateTime.UtcNow.AddDays(1) }]
        }, settings, preparation, null));
    }

    [Fact]
    public async Task MalformedMetadataIsAMissAndFailedSynthesisCanBeRetried()
    {
        var tts = new FakeTts { Fail = true };
        await Assert.ThrowsAsync<IOException>(() => Service(tts).GenerateWavBytesAsync(Request()));
        Assert.False(Directory.Exists(_folder));
        tts.Fail = false;
        await Service(tts).GenerateWavBytesAsync(Request());
        var metadata = Assert.Single(Directory.GetFiles(_folder, "*.source.json", SearchOption.AllDirectories));
        await File.WriteAllTextAsync(metadata, "{truncated");
        await Service(tts).GenerateWavBytesAsync(Request());
        Assert.Equal(2, tts.SynthesisCalls); // one failed call, one success; synthesis survives the lost mapping
        Assert.Equal(3, tts.EmphasisCalls);
    }

    [Fact]
    public async Task ManifestPreservesArtifactOrderAndRejectsMissingDependencies()
    {
        var repository = Repository();
        var first = AudioCacheKey.Hash("first");
        var second = AudioCacheKey.Hash("second");
        var source = AudioCacheKey.Hash("source");
        await repository.SaveAsync(first, new MemoryStream(Wav()));
        await repository.SaveAsync(second, new MemoryStream(Wav()));
        await repository.SaveManifestAsync(new(1, source, AudioCacheKey.Hash("preparation"), [second, first, second]));
        Assert.Equal(new[] { second, first, second }, (await Repository().ReadManifestAsync(source))!.ArtifactKeys);
        File.Delete(Path.Combine(_folder, "audio", "v1", first + ".wav"));
        Assert.Null(await Repository().ReadManifestAsync(source));
    }

    [Fact]
    public async Task CancelledWaiterDoesNotBreakCoordination()
    {
        var key = AudioCacheKey.Hash("lock");
        var repository = Repository();
        using (await repository.AcquireAsync(key))
        {
            using var cancelled = new CancellationTokenSource();
            var waiting = Repository().AcquireAsync(key, cancelled.Token);
            await cancelled.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        }
        using var next = await Repository().AcquireAsync(key);
    }

    public void Dispose() { if (Directory.Exists(_folder)) Directory.Delete(_folder, true); }

    private sealed class FakeTts : ITtsService
    {
        public int SynthesisCalls;
        public int EmphasisCalls;
        public string Prompt = "Emphasis prompt";
        public bool Fail;
        public AudioFormat OutputAudioFormat => AudioFormat.Wav;
        public bool SupportsTextEmphasis(string? modelId) => true;
        public IReadOnlyList<PromptMessage> GetEmphasisMessages(string text) => [new() { Role = PromptMessageRole.System, Message = Prompt }, new() { Role = PromptMessageRole.User, Message = text }];
        public async Task<string> EmphasizeTextAsync(TtsRequest request, Func<CancellationToken, ValueTask<ITextGenerationService>> textGenerationServiceFactory, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref EmphasisCalls);
            await Task.Delay(10, cancellationToken);
            return "Emphasized " + request.Message;
        }
        public async Task<byte[]> GenerateAudioAsync(TtsRequest request, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref SynthesisCalls);
            if (Fail) throw new IOException("Simulated provider failure");
            await Task.Delay(10, cancellationToken);
            return Wav();
        }
        public Task<Stream> GenerateAudioStreamAsync(TtsRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<TtsModelDto>> GetModelsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<decimal?> GetBalanceUsdAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class Integrations : IIntegrationsService
    {
        public ValueTask<IntegrationsConfig> GetConfigAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(new IntegrationsConfig());
        public Task UpdateConfigAsync(IntegrationsConfig config, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class Resolver : ITextGenerationServiceResolver
    {
        public ValueTask<ITextGenerationService> GetConfiguredServiceAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class Legacy : IAudioRepository
    {
        public Task<byte[]>? GetAudioFileAsync(AudioGenerationParameters parameters, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Legacy cache must not be read");
        public Task SaveAudioFileAsync(AudioGenerationParameters parameters, byte[] audioData, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Legacy cache must not be written");
    }
}
