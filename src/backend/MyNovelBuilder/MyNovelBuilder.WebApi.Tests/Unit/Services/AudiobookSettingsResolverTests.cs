using Microsoft.Extensions.DependencyInjection;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Helpers;
using MyNovelBuilder.WebApi.Models.AudioGeneration;
using MyNovelBuilder.WebApi.Models.Integrations;
using MyNovelBuilder.WebApi.Models.Novels;
using MyNovelBuilder.WebApi.Models.Tts;
using MyNovelBuilder.WebApi.Services;
using MyNovelBuilder.WebApi.Services.TextGeneration;
using MyNovelBuilder.WebApi.Services.Tts;

namespace MyNovelBuilder.WebApi.Tests.Unit.Services;

public class AudiobookSettingsResolverTests
{
    [Theory]
    [InlineData(TtsProvider.OpenRouter, "custom-character", true)]
    [InlineData(TtsProvider.OpenRouter, "voice-a", true)]
    [InlineData(TtsProvider.PocketTts, "custom-character", false)]
    [InlineData(TtsProvider.PocketTts, "voice-a", true)]
    public async Task SnapshotValidatesCharacterVoicesUsingProviderPolicy(
        TtsProvider provider, string characterVoiceId, bool accepted)
    {
        using var baseFactory = new MyNovelBuilder.WebApi.Tests.Factories.TestWebApplicationFactory<Program>();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddKeyedSingleton<ITtsService>(provider, new FakeTtsService());
            services.AddKeyedSingleton<ITextGenerationService>(TextGenerationProvider.OpenRouter, new FakeTextService());
            services.AddSingleton<IIntegrationsService>(new FakeIntegrations(new IntegrationsConfig
            {
                TtsProvider = provider, TtsModelId = "model-a", TtsVoiceId = "voice-a",
                TextGenerationProvider = TextGenerationProvider.OpenRouter, TextGenerationModelId = "text-a"
            }));
        }));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MyNovelBuilder.WebApi.Data.AppDbContext>();
        var character = new CompendiumRecord
        {
            Name = "Alice", Type = CompendiumRecordType.Character, AlwaysIncluded = true,
            CharacterVoiceAssignments = [new CharacterVoiceAssignment
            {
                Provider = provider, ModelId = "model-a", VoiceId = characterVoiceId
            }]
        };
        var novel = new Novel
        {
            Title = "Character voices",
            Compendia = [new Compendium { Name = "Characters", Records = [character] }]
        };
        var prompt = new Prompt
        {
            Name = "Immersive", Type = PromptType.PrepareImmersiveTts,
            Messages = [new() { Role = PromptMessageRole.User, Message = "{{speakerOptions}} {{sectionText}}" }]
        };
        db.Novels.Add(novel);
        db.Prompts.Add(prompt);
        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<INovelService>().UpdateProseAsync(novel.Id,
            new Prose { Chapters = [new Chapter
            {
                Title = "One", Sections = [new Section { Text = "Alice speaks." }]
            }] });
        var builder = scope.ServiceProvider.GetRequiredService<AudiobookSnapshotBuilder>();
        var request = new AudiobookSettingsRequest(EnableImmersive: true, ImmersivePromptId: prompt.Id);

        if (accepted)
        {
            var snapshot = await builder.BuildAsync(novel.Id, request);
            var assignment = Assert.Single(Assert.Single(Assert.Single(snapshot.Chapters).Sections).VoiceAssignments);
            Assert.Equal(characterVoiceId, assignment.VoiceId);
        }
        else
        {
            var error = await Assert.ThrowsAsync<MyNovelBuilder.WebApi.Exceptions.ApiException>(
                () => builder.BuildAsync(novel.Id, request));
            Assert.Equal($"Character voice '{characterVoiceId}' is not available for model 'model-a'.", error.Message);
        }
    }

    [Fact]
    public async Task OpenRouterAcceptsCustomVoiceIdsOutsideItsCatalogue()
    {
        var resolver = CreateResolver(new IntegrationsConfig
        {
            TtsProvider = TtsProvider.OpenRouter, TtsModelId = "model-a", TtsVoiceId = "933563129e564b19a115bedd57b7406a"
        });
        var result = await resolver.ResolveAsync(new AudiobookSettingsRequest());
        Assert.Equal("933563129e564b19a115bedd57b7406a", result.VoiceId);
    }

    [Fact]
    public async Task ExplicitFalseAndZeroRemainFrozenWithoutChangingDefaults()
    {
        var defaults = new IntegrationsConfig
        {
            TtsProvider = TtsProvider.PocketTts,
            TtsModelId = "model-a",
            TtsVoiceId = "voice-a",
            TtsEnableTextEmphasis = true,
            TtsEnableImmersive = true,
            TtsImmersivePauseMs = 150,
            TextGenerationModelId = "text-a"
        };
        var resolver = CreateResolver(defaults);

        var result = await resolver.ResolveAsync(new AudiobookSettingsRequest(
            EnableTextEmphasis: false,
            EnableImmersive: false,
            ImmersivePauseMs: 0));

        Assert.False(result.EnableTextEmphasis);
        Assert.False(result.EnableImmersive);
        Assert.Equal(0, result.ImmersivePauseMs);
        Assert.True(defaults.TtsEnableTextEmphasis);
        Assert.True(defaults.TtsEnableImmersive);
        Assert.Equal(150, defaults.TtsImmersivePauseMs);
    }

    [Fact]
    public async Task MismatchedVoiceIsRejectedBeforeSynthesis()
    {
        var resolver = CreateResolver(new IntegrationsConfig
        {
            TtsProvider = TtsProvider.PocketTts,
            TtsModelId = "model-a",
            TtsVoiceId = "voice-a",
            TextGenerationModelId = "text-a"
        });

        await Assert.ThrowsAsync<MyNovelBuilder.WebApi.Exceptions.ApiException>(() =>
            resolver.ResolveAsync(new AudiobookSettingsRequest(VoiceId: "other")));
    }

    [Fact]
    public async Task SnapshotKeepsSavedSpeechAndSettingsAfterSourceChanges()
    {
        var defaults = new IntegrationsConfig
        {
            TtsProvider = TtsProvider.PocketTts,
            TtsModelId = "model-a",
            TtsVoiceId = "voice-a",
            TextGenerationModelId = "text-a"
        };
        var settings = await CreateResolver(defaults).ResolveAsync(new AudiobookSettingsRequest());
        var novel = new Novel { Title = "Before" };
        var prose = new Prose { Chapters = [new Chapter
        {
            Title = "Chapter A",
            Sections = [new Section { Text = "<p>Original speech.</p>" }]
        }] };

        var snapshot = AudiobookSnapshotBuilder.Capture(novel, prose, settings);
        prose.Chapters[0].Sections[0].Text = "<p>Later speech.</p>";
        defaults.TtsVoiceId = "later-voice";

        Assert.Equal("Original speech.", snapshot.Chapters[0].Sections[0].SpeechText);
        Assert.Equal("voice-a", snapshot.Settings.VoiceId);
        Assert.Equal("Before", snapshot.Title);
    }

    [Theory]
    [InlineData("<p>Hello&nbsp;world!</p><p>Again.</p>", "Hello world!\n\nAgain.")]
    [InlineData("<div>Hello<br>there</div><script>bad()</script>", "Hello\nthere")]
    [InlineData("<p><strong>Hi</strong> &amp; bye.</p>", "Hi & bye.")]
    [InlineData("<span title=\"a > b\">Hello</span>", "Hello")]
    [InlineData("<p>A &lt; B &amp; C</p>", "A < B & C")]
    [InlineData("<template><template>hidden</template>also hidden</template><p>Spoken</p>", "Spoken")]
    [InlineData("<!-- comment --><p>First</p><p>Second</p><script>unfinished", "First\n\nSecond")]
    public void SpeechNormalizerPreservesSpokenBoundaries(string html, string expected)
    {
        Assert.Equal(expected, AudiobookSpeechTextNormalizer.Normalize(html));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task AuxiliaryModelMustBelongToSelectedProvider(bool emphasis, bool immersive)
    {
        var resolver = CreateResolver(new IntegrationsConfig
        {
            TtsProvider = TtsProvider.ElevenLabs, TtsModelId = "eleven_v3", TtsVoiceId = "voice-a",
            TextGenerationModelId = "openrouter/auto"
        });
        var error = await Assert.ThrowsAsync<MyNovelBuilder.WebApi.Exceptions.ApiException>(() => resolver.ResolveAsync(
            new AudiobookSettingsRequest(TextGenerationProvider: TextGenerationProvider.GoogleGenAi,
                EnableTextEmphasis: emphasis, EnableImmersive: immersive, ImmersivePromptId: Guid.NewGuid())));
        Assert.Contains("not available", error.Message);
    }

    [Fact]
    public async Task UnusedAuxiliarySettingsDoNotPreventNarration()
    {
        var result = await CreateResolver(new IntegrationsConfig
        {
            TtsProvider = TtsProvider.PocketTts, TtsModelId = "model-a", TtsVoiceId = "voice-a",
            TextGenerationModelId = "", TextGenerationProvider = (TextGenerationProvider)999
        }).ResolveAsync(new AudiobookSettingsRequest());
        Assert.False(result.EnableTextEmphasis);
        Assert.False(result.EnableImmersive);
    }

    [Fact]
    public async Task ImmersivePromptUsesNormalizedFrozenSpeechAndContext()
    {
        var settings = await CreateResolver(new IntegrationsConfig
        {
            TtsProvider = TtsProvider.PocketTts, TtsModelId = "model-a", TtsVoiceId = "voice-a",
            TextGenerationModelId = "text-a"
        }).ResolveAsync(new AudiobookSettingsRequest(EnableImmersive: true, ImmersivePromptId: Guid.NewGuid()));
        var prose = new Prose { Chapters = [new Chapter
        {
            Title = "Chapter",
            Sections = [new Section { Text = "<p>Earlier<br>speech</p><style>hidden</style>" },
                new Section { Text = "<p>Hello<br>world &lt;3 &amp; bye</p><script>secret()</script>" }]
        }] };
        var prompt = new Prompt
        {
            Name = "Immersive", Type = PromptType.PrepareImmersiveTts,
            Messages = [new() { Role = PromptMessageRole.User,
                Message = "{{sectionText}}\nCHAPTER:{{wholeChapter}}\nEARLIER:{{storySoFar}}" }]
        };
        var snapshot = AudiobookSnapshotBuilder.Capture(new Novel { Title = "Novel" }, prose, settings, prompt);
        var section = snapshot.Chapters[0].Sections[1];
        var message = Assert.Single(section.ImmersivePrompt).Message;
        Assert.StartsWith("<tts-source>" + section.SpeechText + "</tts-source>", message);
        Assert.Contains("Hello\nworld <3 & bye", message);
        Assert.Contains("Earlier\nspeech", message);
        Assert.DoesNotContain("secret", message);
        Assert.DoesNotContain("hidden", message);
        prose.Chapters[0].Sections[1].Text = "Changed";
        prompt.Messages = [];
        Assert.Equal(message, Assert.Single(section.ImmersivePrompt).Message);
    }

    private static AudiobookSettingsResolver CreateResolver(IntegrationsConfig defaults)
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<ITtsService>(TtsProvider.PocketTts, new FakeTtsService());
        services.AddKeyedSingleton<ITtsService>(TtsProvider.ElevenLabs, new FakeTtsService());
        services.AddKeyedSingleton<ITtsService>(TtsProvider.OpenRouter, new FakeTtsService());
        services.AddKeyedSingleton<ITextGenerationService>(TextGenerationProvider.OpenRouter,
            new FakeTextService());
        services.AddKeyedSingleton<ITextGenerationService>(TextGenerationProvider.GoogleGenAi,
            new FakeTextService());
        return new AudiobookSettingsResolver(new FakeIntegrations(defaults), services.BuildServiceProvider(), new NoRecordedVoices());
    }

    private sealed class FakeIntegrations(IntegrationsConfig config) : IIntegrationsService
    {
        public ValueTask<IntegrationsConfig> GetConfigAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(config);

        public Task UpdateConfigAsync(IntegrationsConfig config, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Resolution must not update defaults.");
    }

    private sealed class FakeTtsService : ITtsService
    {
        public AudioFormat OutputAudioFormat => AudioFormat.Wav;
        public bool SupportsTextEmphasis(string? modelId) => true;
        public Task<IEnumerable<TtsModelDto>> GetModelsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IEnumerable<TtsModelDto>>([new TtsModelDto { ModelId = "eleven_v3", Name = "Eleven v3", Voices = [new TtsVoiceDto { VoiceId = "voice-a", Name = "Voice A", Language = WritingLanguage.English }] }, new TtsModelDto
            {
                ModelId = "model-a",
                Name = "Model A",
                Voices = [new TtsVoiceDto { VoiceId = "voice-a", Name = "Voice A", Language = WritingLanguage.English }]
            }]);
        public Task<byte[]> GenerateAudioAsync(TtsRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Synthesis must not run during resolution.");
        public Task<Stream> GenerateAudioStreamAsync(TtsRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Synthesis must not run during resolution.");
        public Task<decimal?> GetBalanceUsdAsync(CancellationToken cancellationToken = default) => Task.FromResult<decimal?>(null);
    }

    private sealed class FakeTextService : ITextGenerationService
    {
        public Task<string> GenerateAsync(string model, IEnumerable<MyNovelBuilder.WebApi.Models.Prompts.PromptMessage> messages,
            MyNovelBuilder.WebApi.Models.TextGeneration.StructuredOutputOptions? structuredOutputOptions = null,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public async IAsyncEnumerable<string> GenerateStreamedAsync(string model,
            IEnumerable<MyNovelBuilder.WebApi.Models.Prompts.PromptMessage> messages,
            MyNovelBuilder.WebApi.Models.TextGeneration.StructuredOutputOptions? structuredOutputOptions = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }
        public Task<string> DescribeImageAsync(string model,
            IEnumerable<MyNovelBuilder.WebApi.Models.Prompts.PromptMessage> messages,
            byte[] imageBytes, string imageMimeType, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();
        public Task<IEnumerable<MyNovelBuilder.WebApi.Models.TextGeneration.TextGenerationModelInfo>> GetAvailableModelsAsync(
            CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<MyNovelBuilder.WebApi.Models.TextGeneration.TextGenerationModelInfo>>(
                [new() { Id = "text-a" }]);
    }
}
