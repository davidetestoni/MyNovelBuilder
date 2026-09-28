using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Models.Integrations;
using MyNovelBuilder.WebApi.Models.AudioGeneration;
using MyNovelBuilder.WebApi.Models.Prompts;
using MyNovelBuilder.WebApi.Models.TextGeneration;
using MyNovelBuilder.WebApi.Models.Tts;
using MyNovelBuilder.WebApi.Services;
using MyNovelBuilder.WebApi.Services.TextGeneration;

namespace MyNovelBuilder.WebApi.Tests.Unit.Services;

public class ImmersiveTtsServiceTests
{
    [Fact]
    public async Task PrepareDebugAsync_FallsBackToNarrator_WhenCharacterHasNoMatchingVoiceAssignment()
    {
        var characterId = Guid.NewGuid();
        var service = new ImmersiveTtsService(
            new FakeNovelPromptCreatorService(new ProcessedPrompt
            {
                Messages =
                [
                    new PromptMessage
                    {
                        Role = PromptMessageRole.User,
                        Message = "Plan immersive chunks."
                    }
                ],
                IncludedCompendiumRecordIds = [characterId]
            }),
            new FakeTextGenerationServiceResolver(new FakeTextGenerationService(
                $$"""
                [
                  {
                    "speakerKind": "character",
                    "speakerName": "Vera",
                    "characterRecordId": "{{characterId}}",
                    "text": "I'm tired."
                  }
                ]
                """)),
            new FakeIntegrationsService(new IntegrationsConfig
            {
                TextGenerationProvider = TextGenerationProvider.OpenRouter,
                TextGenerationModelId = "openrouter/planner-model",
                TtsProvider = TtsProvider.ElevenLabs,
                TtsModelId = "eleven-v3",
                TtsVoiceId = "narrator-voice",
                TtsImmersivePauseMs = 275
            }),
            new FakeCompendiumRecordService(
            [
                new CompendiumRecord
                {
                    Id = characterId,
                    Name = "Vera",
                    Type = CompendiumRecordType.Character,
                    CharacterVoiceAssignments =
                    [
                        new CharacterVoiceAssignment
                        {
                            Provider = TtsProvider.ElevenLabs,
                            ModelId = "different-model",
                            VoiceId = "character-voice"
                        }
                    ]
                }
            ]),
            new RecordingTtsAudioGenerationService(CreatePcmWavBytes([1, 2])),
            NullLogger<ImmersiveTtsService>.Instance, new ServiceCollection().BuildServiceProvider(), new NoRecordedVoices());

        var response = await service.PrepareDebugAsync(new ImmersiveTtsRequestDto
        {
            NovelId = Guid.NewGuid(),
            PromptId = Guid.NewGuid(),
            ChapterIndex = 0,
            SectionIndex = 0
        });

        Assert.Equal(TtsProvider.ElevenLabs, response.Provider);
        Assert.Equal("eleven-v3", response.TtsModelId);
        Assert.Equal("openrouter/planner-model", response.TextGenerationModelId);
        Assert.Equal(275, response.PauseMs);

        var chunk = Assert.Single(response.Chunks);
        Assert.Equal("narrator", chunk.SpeakerKind);
        Assert.Equal("narrator-voice", chunk.VoiceId);
        Assert.True(chunk.IsNarratorFallback);
        Assert.Equal("I'm tired.", chunk.Text);
    }

    [Fact]
    public async Task GenerateStreamAsync_InsertsConfiguredPause_AndUsesConfiguredTextModel()
    {
        var recordingTtsService = new RecordingTtsAudioGenerationService(CreatePcmWavBytes([10, 20]));
        var service = new ImmersiveTtsService(
            new FakeNovelPromptCreatorService(new ProcessedPrompt
            {
                Messages =
                [
                    new PromptMessage
                    {
                        Role = PromptMessageRole.User,
                        Message = "Plan immersive chunks."
                    }
                ],
                IncludedCompendiumRecordIds = []
            }),
            new FakeTextGenerationServiceResolver(new FakeTextGenerationService(
                """
                [
                  {
                    "speakerKind": "narrator",
                    "speakerName": "Narrator",
                    "text": "First."
                  },
                  {
                    "speakerKind": "narrator",
                    "speakerName": "Narrator",
                    "text": "Second."
                  }
                ]
                """)),
            new FakeIntegrationsService(new IntegrationsConfig
            {
                TextGenerationProvider = TextGenerationProvider.OpenRouter,
                TextGenerationModelId = "openrouter/planner-model",
                TtsProvider = TtsProvider.ElevenLabs,
                TtsModelId = "eleven-v3",
                TtsVoiceId = "narrator-voice",
                TtsImmersivePauseMs = 100
            }),
            new FakeCompendiumRecordService([]),
            recordingTtsService,
            NullLogger<ImmersiveTtsService>.Instance, new ServiceCollection().BuildServiceProvider(), new NoRecordedVoices());

        await using var stream = await service.GenerateStreamAsync(new ImmersiveTtsRequestDto
        {
            NovelId = Guid.NewGuid(),
            PromptId = Guid.NewGuid(),
            ChapterIndex = 0,
            SectionIndex = 0
        });
        await using var output = new MemoryStream();
        await stream.CopyToAsync(output);
        var bytes = output.ToArray();

        Assert.Equal(2, recordingTtsService.Requests.Count);
        Assert.All(recordingTtsService.Requests, request =>
        {
            Assert.Equal("openrouter/planner-model", request.TextGenerationModelId);
            Assert.Equal("narrator-voice", request.VoiceId);
            Assert.Equal("eleven-v3", request.TtsModelId);
            Assert.Equal(TtsProvider.ElevenLabs, request.Provider);
        });

        Assert.Equal(44 + 2 + 4800 + 2, bytes.Length);
        Assert.All(bytes.Skip(46).Take(4800), value => Assert.Equal(0, value));
    }

    [Fact]
    public async Task FrozenRenderingUsesSnapshotPromptVoicesAndZeroPauseAfterDefaultsChange()
    {
        var characterId = Guid.NewGuid();
        var planner = new FakeTextGenerationService($$"""
            [{"speakerKind":"character","characterRecordId":"{{characterId}}","text":"First."},
             {"speakerKind":"narrator","text":"Second."}]
            """);
        using var services = new ServiceCollection()
            .AddKeyedSingleton<ITextGenerationService>(TextGenerationProvider.OpenRouter, planner)
            .BuildServiceProvider();
        var config = new IntegrationsConfig
        {
            TtsProvider = TtsProvider.PocketTts, TtsModelId = "changed-model", TtsVoiceId = "changed-voice",
            TtsEnableTextEmphasis = true, TtsImmersivePauseMs = 900, TextGenerationProvider = TextGenerationProvider.GoogleGenAi,
            TextGenerationModelId = "changed-text-model"
        };
        var tts = new RecordingTtsAudioGenerationService(CreatePcmWavBytes([10, 20]));
        var livePrompt = new FakeNovelPromptCreatorService(new ProcessedPrompt { Messages = [], IncludedCompendiumRecordIds = [] });
        var liveRecords = new FakeCompendiumRecordService([]);
        var service = new ImmersiveTtsService(livePrompt,
            new FakeTextGenerationServiceResolver(new FakeTextGenerationService("invalid live plan")),
            new FakeIntegrationsService(config), liveRecords, tts,
            NullLogger<ImmersiveTtsService>.Instance, services, new NoRecordedVoices());
        var settings = new ResolvedAudiobookSettings(TtsProvider.ElevenLabs, "frozen-model", "narrator",
            false, true, 0, TextGenerationProvider.OpenRouter, "frozen-text-model", Guid.NewGuid(), null, null);
        var section = new AudiobookSectionSnapshot(0, "First. Second.",
            [new(PromptMessageRole.User, "Frozen prompt")],
            [new(characterId, "character", DateTime.UtcNow, null)]);
        await using var stream = await service.GenerateStreamAsync(new AudiobookRenderRequest(Guid.NewGuid(), 0, settings, section));
        using var output = new MemoryStream();
        await stream.CopyToAsync(output);
        Assert.Equal(48, output.Length); // WAV header + two samples, no pause.
        Assert.Equal("frozen-text-model", planner.Model);
        Assert.Equal("Frozen prompt", Assert.Single(planner.Messages).Message);
        Assert.Equal(0, livePrompt.Calls);
        Assert.Equal(0, liveRecords.GetByIdsCalls);
        Assert.Equal(new[] { "character", "narrator" }, tts.Requests.Select(request => request.VoiceId));
        Assert.All(tts.Requests, request =>
        {
            Assert.NotNull(request.ResolvedOptions);
            Assert.False(request.ResolvedOptions.EnableTextEmphasis);
            Assert.Equal("frozen-model", request.ResolvedOptions.ModelId);
            Assert.Equal(TextGenerationProvider.OpenRouter, request.ResolvedOptions.TextGenerationProvider);
        });
    }

    private static byte[] CreatePcmWavBytes(byte[] pcmBytes, int sampleRate = 24000, short channels = 1, short bitsPerSample = 16)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

        var blockAlign = (short)(channels * (bitsPerSample / 8));
        var byteRate = sampleRate * blockAlign;

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + pcmBytes.Length);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(pcmBytes.Length);
        writer.Write(pcmBytes);

        return stream.ToArray();
    }

    private sealed class FakeNovelPromptCreatorService(ProcessedPrompt processedPrompt) : INovelPromptCreatorService
    {
        public int Calls { get; private set; }
        public Task<ProcessedPrompt> CreatePromptAsync(
            GenerateTextRequestDto request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(processedPrompt);
        }
    }

    private sealed class FakeTextGenerationServiceResolver(ITextGenerationService service) : ITextGenerationServiceResolver
    {
        public ValueTask<ITextGenerationService> GetConfiguredServiceAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(service);
        }
    }

    private sealed class FakeTextGenerationService(string generatedText) : ITextGenerationService
    {
        public string? Model { get; private set; }
        public List<PromptMessage> Messages { get; private set; } = [];
        public Task<string> GenerateAsync(
            string model,
            IEnumerable<PromptMessage> messages,
            StructuredOutputOptions? structuredOutputOptions = null,
            CancellationToken cancellationToken = default)
        {
            Model = model;
            Messages = messages.ToList();
            return Task.FromResult(generatedText);
        }

        public async IAsyncEnumerable<string> GenerateStreamedAsync(
            string model,
            IEnumerable<PromptMessage> messages,
            StructuredOutputOptions? structuredOutputOptions = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return generatedText;
            await Task.CompletedTask;
        }

        public Task<string> DescribeImageAsync(
            string model,
            IEnumerable<PromptMessage> messages,
            byte[] imageBytes,
            string imageMimeType,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IEnumerable<TextGenerationModelInfo>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeIntegrationsService(IntegrationsConfig config) : IIntegrationsService
    {
        public ValueTask<IntegrationsConfig> GetConfigAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(config);
        }

        public Task UpdateConfigAsync(IntegrationsConfig updatedConfig, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeCompendiumRecordService(IEnumerable<CompendiumRecord> records) : ICompendiumRecordService
    {
        public int GetByIdsCalls { get; private set; }
        private readonly IReadOnlyDictionary<Guid, CompendiumRecord> _records = records.ToDictionary(record => record.Id);

        public Task<CompendiumRecord> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_records[id]);
        }

        public Task<IEnumerable<CompendiumRecord>> GetByCompendiumIdAsync(Guid compendiumId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IEnumerable<CompendiumRecord>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
        {
            GetByIdsCalls++;
            var result = ids.Where(_records.ContainsKey).Select(id => _records[id]);
            return Task.FromResult<IEnumerable<CompendiumRecord>>(result.ToList());
        }

        public Task<IEnumerable<CompendiumRecord>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task CreateAsync(CompendiumRecord compendiumRecord, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task UpdateAsync(CompendiumRecord compendiumRecord, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IEnumerable<MyNovelBuilder.WebApi.Models.Media.MediaRef>> GetGalleryMediaAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task UploadMediaAsync(Guid id, IFormFile file, bool isCurrent = false, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SetCurrentImageAsync(Guid id, Guid imageId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteMediaAsync(Guid id, Guid mediaId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class RecordingTtsAudioGenerationService(byte[] wavBytes) : ITtsAudioGenerationService
    {
        public List<TextToSpeechGenerationRequest> Requests { get; } = [];

        public Task<byte[]> GenerateWavBytesAsync(
            TextToSpeechGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(wavBytes);
        }

        public Task<Stream> GenerateWavStreamAsync(
            TextToSpeechGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult<Stream>(new MemoryStream(wavBytes));
        }
    }
}
