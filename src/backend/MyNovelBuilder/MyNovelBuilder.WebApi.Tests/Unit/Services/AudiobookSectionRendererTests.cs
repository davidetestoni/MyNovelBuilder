using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MyNovelBuilder.WebApi.Dtos.Generate;
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
using NAudio.Wave;

namespace MyNovelBuilder.WebApi.Tests.Unit.Services;

public sealed class AudiobookSectionRendererTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ModesProduceFinitePcmAndCompletedRepeatSkipsAllProviders(bool immersive, bool emphasis)
    {
        using var fixture = new Fixture();
        var request = Fixture.Request("Opening. Reply.", immersive, emphasis);
        fixture.Text.PlannerResponse = Plan(("Opening. ", null), ("Reply.", Guid.NewGuid()));
        var result = await fixture.Renderer.RenderAsync(request);
        var length = await fixture.PcmLength(result.ArtifactKey);
        Assert.Equal(immersive ? 2 * 480 + 150 * 48 : 480, length);
        Assert.All(fixture.Tts.Requests, r => Assert.Equal("narrator", r.VoiceId)); // unassigned character fallback
        var calls = fixture.Tts.Requests.Count;
        var textCalls = fixture.Text.Calls;
        // Reconstruct services and snapshot, losing all text-bearing in-memory state.
        fixture.Restart();
        var repeated = await fixture.Renderer.RenderAsync(request with { Section = request.Section with { } });
        Assert.True(repeated.Reused);
        Assert.Equal(result.ArtifactKey, repeated.ArtifactKey);
        Assert.Equal(calls, fixture.Tts.Requests.Count);
        Assert.Equal(textCalls, fixture.Text.Calls);
        foreach (var path in Directory.GetFiles(fixture.Folder, "*.json", SearchOption.AllDirectories))
        {
            var json = await File.ReadAllTextAsync(path);
            Assert.DoesNotContain("Opening", json);
            Assert.DoesNotContain("Reply", json);
        }
    }

    [Fact]
    public async Task AssignedCharacterVoiceAndPauseChangesReuseAllSpeech()
    {
        using var fixture = new Fixture();
        var id = Guid.NewGuid();
        var request = Fixture.Request("Opening. Reply.", true, false);
        request = request with { Section = request.Section with { VoiceAssignments = [new(id, "character", DateTime.UtcNow, null)] } };
        fixture.Text.PlannerResponse = Plan(("Opening. ", null), ("Reply.", id));
        var first = await fixture.Renderer.RenderAsync(request);
        Assert.Equal(new[] { "narrator", "character" }, fixture.Tts.Requests.Select(r => r.VoiceId));
        var calls = fixture.Text.Calls;
        var second = await fixture.Renderer.RenderAsync(request with { Settings = request.Settings with { ImmersivePauseMs = 0 } });
        Assert.NotEqual(first.ArtifactKey, second.ArtifactKey);
        Assert.Equal(960, await fixture.PcmLength(second.ArtifactKey));
        Assert.Equal(2, fixture.Tts.Requests.Count);
        Assert.Equal(calls, fixture.Text.Calls);
        fixture.DeleteAudio(second.ArtifactKey);
        var rebuilt = await fixture.Renderer.RenderAsync(request with { Settings = request.Settings with { ImmersivePauseMs = 0 } });
        Assert.Equal(960, await fixture.PcmLength(rebuilt.ArtifactKey));
        Assert.Equal(2, fixture.Tts.Requests.Count);
        Assert.Equal(calls, fixture.Text.Calls);
    }

    [Fact]
    public async Task OversizedNarrationPreservesAllCharactersAndHasNoSplittingPauses()
    {
        using var fixture = new Fixture();
        var text = string.Concat(Enumerable.Range(0, 300).Select(i => $"Paragraph {i}: a spoken sentence with emoji 🌟.\n\n"));
        var result = await fixture.Renderer.RenderAsync(Fixture.Request(text, false, false));
        Assert.Equal(text, string.Concat(fixture.Tts.Requests.Select(r => r.Message)));
        Assert.All(fixture.Tts.Requests, r => Assert.InRange(r.Message.Length, 1, 1000));
        Assert.Equal(fixture.Tts.Requests.Count * 480, await fixture.PcmLength(result.ArtifactKey));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailureOrCancellationRetainsPlanEmphasisAndCompletedChunks(bool cancel)
    {
        using var fixture = new Fixture();
        var request = Fixture.Request("First. Second. Third.", true, true);
        fixture.Text.PlannerResponse = Plan(("First. ", null), ("Second. ", null), ("Third.", null));
        using var cancellation = new CancellationTokenSource();
        fixture.Tts.BeforeCall = number =>
        {
            if (number != 2) return;
            if (cancel) { cancellation.Cancel(); cancellation.Token.ThrowIfCancellationRequested(); }
            throw new IOException("Provider interrupted");
        };
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.Renderer.RenderAsync(request, cancellation.Token));
        Assert.Equal(1, fixture.Text.PlannerCalls);
        Assert.Equal(2, fixture.Text.EmphasisCalls);
        fixture.Tts.BeforeCall = null;
        var result = await fixture.Renderer.RenderAsync(request);
        Assert.Equal(1, fixture.Text.PlannerCalls);
        Assert.Equal(3, fixture.Text.EmphasisCalls); // only the unvisited third chunk is emphasized
        Assert.Equal(4, fixture.Tts.Requests.Count); // first successful chunk was not synthesized again
        Assert.Equal(3 * 480 + 2 * 150 * 48, await fixture.PcmLength(result.ArtifactKey));
        Assert.Empty(Directory.GetFiles(fixture.Folder, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task ExpandedEmphasisIsSplitAndCheckpointedWithoutLosingTags()
    {
        using var fixture = new Fixture();
        fixture.Text.ExpandEmphasis = true;
        var result = await fixture.Renderer.RenderAsync(Fixture.Request("A short sentence.", false, true));
        Assert.True(fixture.Tts.Requests.Count > 1);
        Assert.All(fixture.Tts.Requests, r => Assert.InRange(r.Message.Length, 1, 1000));
        Assert.Equal(fixture.Text.LastEmphasis, string.Concat(fixture.Tts.Requests.Select(r => r.Message)));
        Assert.All(fixture.Tts.Requests, r => Assert.Equal(r.Message.Count(c => c == '['), r.Message.Count(c => c == ']')));
        Assert.Equal(480 * fixture.Tts.Requests.Count, await fixture.PcmLength(result.ArtifactKey));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("[null]")]
    [InlineData("[{\"text\":\"Omitted source\"}]")]
    public async Task InvalidPlanFailsWithoutNarratorFallbackAndCanBeRetried(string invalid)
    {
        using var fixture = new Fixture();
        var request = Fixture.Request("Original speech.", true, false);
        fixture.Text.PlannerResponse = invalid;
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.Renderer.RenderAsync(request));
        Assert.Empty(fixture.Tts.Requests);
        fixture.Text.PlannerResponse = Plan(("Original speech.", null));
        await fixture.Renderer.RenderAsync(request);
        Assert.Equal(2, fixture.Text.PlannerCalls);
        Assert.Single(fixture.Tts.Requests);
    }

    [Fact]
    public async Task IncompleteEmphasisChunksResumeInMemoryAndCorruptUnitRebuildsFromCheckpoints()
    {
        using var fixture = new Fixture();
        fixture.Text.ExpandEmphasis = true;
        var request = Fixture.Request("Source speech.", false, true);
        fixture.Tts.BeforeCall = n => { if (n == 2) throw new IOException("Interrupted"); };
        await Assert.ThrowsAsync<IOException>(() => fixture.Renderer.RenderAsync(request));
        fixture.Tts.BeforeCall = null;
        var result = await fixture.Renderer.RenderAsync(request);
        Assert.Equal(1, fixture.Text.EmphasisCalls);
        var calls = fixture.Tts.Requests.Count;
        fixture.DeleteAudio(result.ChunkArtifactKeys[0]);
        await fixture.Renderer.RenderAsync(request);
        Assert.Equal(calls, fixture.Tts.Requests.Count);
        Assert.Equal(1, fixture.Text.EmphasisCalls);
    }

    [Fact]
    public async Task PartialPlainSectionReusesCompletedChunksAfterLosingPlans()
    {
        using var fixture = new Fixture();
        var request = Fixture.Request("First. Second.", true, false);
        fixture.Text.PlannerResponse = Plan(("First. ", null), ("Second.", null));
        fixture.Tts.BeforeCall = n => { if (n == 2) throw new IOException("Interrupted"); };
        await Assert.ThrowsAsync<IOException>(() => fixture.Renderer.RenderAsync(request));
        fixture.Tts.BeforeCall = null;
        fixture.Restart();
        await fixture.Renderer.RenderAsync(request with { Section = request.Section with { } });
        Assert.Equal(2, fixture.Text.PlannerCalls); // the unpersisted plan must be reconstructed
        Assert.Equal(3, fixture.Tts.Requests.Count); // first speech is still a durable checkpoint
    }

    [Fact]
    public async Task AssemblyResamplesStereoAndPlacesExactSilenceOnlyBetweenUnits()
    {
        using var fixture = new Fixture();
        fixture.Tts.SampleRate = 48000;
        fixture.Tts.Channels = 2;
        var request = Fixture.Request("First. Second.", true, false);
        fixture.Text.PlannerResponse = Plan(("First. ", null), ("Second.", null));
        var result = await fixture.Renderer.RenderAsync(request);
        Assert.Equal(960 + 150 * 48, await fixture.PcmLength(result.ArtifactKey));
        await using var stream = await fixture.Artifacts.OpenReadAsync(result.ArtifactKey);
        using var reader = new WaveFileReader(stream!);
        var pcm = new byte[(int)reader.Length];
        Assert.Equal(pcm.Length, await reader.ReadAsync(pcm));
        Assert.Contains(pcm[..480], b => b != 0);
        Assert.All(pcm[480..(480 + 150 * 48)], b => Assert.Equal(0, b));
        Assert.Contains(pcm[(480 + 150 * 48)..], b => b != 0);
    }

    [Fact]
    public async Task ConcurrentIdenticalSectionsPlanAndRenderOnce()
    {
        using var fixture = new Fixture();
        var request = Fixture.Request("Spoken text.", true, true);
        fixture.Text.PlannerResponse = Plan(("Spoken text.", null));
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => fixture.Renderer.RenderAsync(request)));
        Assert.All(results, r => Assert.Equal(results[0].ArtifactKey, r.ArtifactKey));
        Assert.Single(fixture.Tts.Requests);
        Assert.Equal(1, fixture.Text.PlannerCalls);
        Assert.Equal(1, fixture.Text.EmphasisCalls);
    }

    [Fact]
    public async Task TogglingEmphasisReusesTheResolvedInMemorySpeakerPlan()
    {
        using var fixture = new Fixture();
        var request = Fixture.Request("Spoken text.", true, false);
        fixture.Text.PlannerResponse = Plan(("Spoken text.", null));
        await fixture.Renderer.RenderAsync(request);
        await fixture.Renderer.RenderAsync(request with { Settings = request.Settings with { EnableTextEmphasis = true } });
        Assert.Equal(1, fixture.Text.PlannerCalls);
        Assert.Equal(1, fixture.Text.EmphasisCalls);
        Assert.Equal(2, fixture.Tts.Requests.Count);
    }

    [Fact]
    public async Task EmptySectionFailsBeforeProviders()
    {
        using var fixture = new Fixture();
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Renderer.RenderAsync(Fixture.Request(" \n ", false, false)));
        Assert.Empty(fixture.Tts.Requests);
        Assert.Equal(0, fixture.Text.Calls);
    }

    private static string Plan(params (string Text, Guid? Character)[] chunks) => JsonSerializer.Serialize(chunks.Select(c => new
    {
        text = c.Text, speakerKind = c.Character is null ? "narrator" : "character",
        speakerName = "Speaker", characterRecordId = c.Character?.ToString()
    }));

    private sealed class Fixture : IDisposable
    {
        public string Folder { get; } = Path.Combine(Path.GetTempPath(), "section-tests-" + Guid.NewGuid());
        public FakeTts Tts { get; } = new();
        public FakeText Text { get; } = new();
        private readonly ServiceProvider _services;
        public IAudioArtifactRepository Artifacts { get; }
        public AudiobookSectionRenderer Renderer { get; private set; } = null!;
        public Fixture()
        {
            _services = new ServiceCollection().AddKeyedSingleton<ITtsService>(TtsProvider.ElevenLabs, Tts)
                .AddKeyedSingleton<ITextGenerationService>(TextGenerationProvider.OpenRouter, Text).BuildServiceProvider();
            Artifacts = new FileSystemAudioArtifactRepository(Microsoft.Extensions.Options.Options.Create(new AppStorageOptions { DataFolder = Folder }));
            Restart();
        }
        public void Restart()
        {
            var storage = Microsoft.Extensions.Options.Options.Create(new AppStorageOptions { DataFolder = Folder });
            var preparations = new AudiobookPreparationCache();
            var assembler = new AudioArtifactAssembler(Artifacts, storage);
            var resolver = new TextResolver(Text);
            var tts = new TtsAudioGenerationService(_services, new Integrations(), new FileSystemWaveAudioRepository(storage),
                resolver, NullLogger<TtsAudioGenerationService>.Instance, new NoRecordedVoices(), Artifacts, assembler, preparations);
            // Frozen preparation must not access live prompts or compendium services.
            var planner = new ImmersiveTtsService(null!, resolver, new Integrations(), null!, tts,
                NullLogger<ImmersiveTtsService>.Instance, _services, new NoRecordedVoices());
            Renderer = new(tts, planner, Artifacts, assembler, preparations, _services);
        }
        public static AudiobookRenderRequest Request(string text, bool immersive, bool emphasis) => new(Guid.NewGuid(), 0,
            new(TtsProvider.ElevenLabs, "model", "narrator", emphasis, immersive, 150,
                TextGenerationProvider.OpenRouter, "auxiliary", Guid.NewGuid(), null, null),
            new(0, text, [new(PromptMessageRole.User, text)], []));
        public async Task<long> PcmLength(string key)
        {
            await using var stream = await Artifacts.OpenReadAsync(key);
            Assert.NotNull(stream);
            using var reader = new WaveFileReader(stream);
            Assert.Equal(24000, reader.WaveFormat.SampleRate);
            Assert.Equal(1, reader.WaveFormat.Channels);
            Assert.Equal(16, reader.WaveFormat.BitsPerSample);
            Assert.Equal(stream.Length - 8, ReadRiffLength(stream));
            return reader.Length;
        }
        private static long ReadRiffLength(Stream stream)
        {
            stream.Position = 4;
            using var reader = new BinaryReader(stream, System.Text.Encoding.ASCII, true);
            return reader.ReadUInt32();
        }
        public void DeleteAudio(string key) => File.Delete(Path.Combine(Folder, "audio", "v1", key + ".wav"));
        public void Dispose() { _services.Dispose(); if (Directory.Exists(Folder)) Directory.Delete(Folder, true); }
    }
    private sealed class FakeTts : ITtsService
    {
        public List<TtsRequest> Requests { get; } = [];
        public Action<int>? BeforeCall;
        public int SampleRate = 24000;
        public int Channels = 1;
        public AudioFormat OutputAudioFormat => AudioFormat.Wav;
        public bool SupportsTextEmphasis(string? modelId) => true;
        public IReadOnlyList<PromptMessage> GetEmphasisMessages(string text) => [new() { Role = PromptMessageRole.User, Message = text }];
        public async Task<string> EmphasizeTextAsync(TtsRequest request,
            Func<CancellationToken, ValueTask<ITextGenerationService>> textGenerationServiceFactory, CancellationToken cancellationToken = default)
            => await (await textGenerationServiceFactory(cancellationToken)).GenerateAsync(request.TextGenerationModelId!, GetEmphasisMessages(request.Message), cancellationToken: cancellationToken);
        public Task<byte[]> GenerateAudioAsync(TtsRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            BeforeCall?.Invoke(Requests.Count);
            cancellationToken.ThrowIfCancellationRequested();
            using var stream = new MemoryStream();
            using (var writer = new WaveFileWriter(stream, new WaveFormat(SampleRate, 16, Channels)))
            {
                var bytes = new byte[SampleRate / 100 * 2 * Channels];
                for (var i = 0; i < bytes.Length; i += 2) bytes[i] = 10;
                writer.Write(bytes, 0, bytes.Length);
            }
            return Task.FromResult(stream.ToArray());
        }
        public Task<Stream> GenerateAudioStreamAsync(TtsRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<TtsModelDto>> GetModelsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<decimal?> GetBalanceUsdAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class FakeText : ITextGenerationService
    {
        public string PlannerResponse = "[]";
        public int PlannerCalls;
        public int EmphasisCalls;
        public int Calls => PlannerCalls + EmphasisCalls;
        public bool ExpandEmphasis;
        public string? LastEmphasis;
        public Task<string> GenerateAsync(string model, IEnumerable<PromptMessage> messages, StructuredOutputOptions? structuredOutputOptions = null, CancellationToken cancellationToken = default)
        {
            Assert.Equal("auxiliary", model);
            if (structuredOutputOptions is not null) { PlannerCalls++; return Task.FromResult(PlannerResponse); }
            EmphasisCalls++;
            LastEmphasis = ExpandEmphasis ? string.Concat(Enumerable.Repeat("[warm] Expanded speech. ", 100)).Trim() : "[warm] " + messages.Last().Message.Trim();
            return Task.FromResult(LastEmphasis);
        }
        public IAsyncEnumerable<string> GenerateStreamedAsync(string model, IEnumerable<PromptMessage> messages, StructuredOutputOptions? structuredOutputOptions = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> DescribeImageAsync(string model, IEnumerable<PromptMessage> messages, byte[] imageBytes, string imageMimeType, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<TextGenerationModelInfo>> GetAvailableModelsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class Integrations : IIntegrationsService
    {
        public ValueTask<IntegrationsConfig> GetConfigAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(new IntegrationsConfig());
        public Task UpdateConfigAsync(IntegrationsConfig config, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class TextResolver(ITextGenerationService text) : ITextGenerationServiceResolver
    {
        public ValueTask<ITextGenerationService> GetConfiguredServiceAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(text);
    }
}
