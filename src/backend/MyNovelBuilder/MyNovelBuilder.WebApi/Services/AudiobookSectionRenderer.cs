using System.Collections.Immutable;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Helpers;
using MyNovelBuilder.WebApi.Models.AudioGeneration;
using MyNovelBuilder.WebApi.Models.Tts;
using MyNovelBuilder.WebApi.Services.Tts;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>Renders one complete frozen section, checkpointing audio and keeping plans only in memory.</summary>
public sealed class AudiobookSectionRenderer(
    IAudiobookTtsService tts,
    IAudiobookImmersivePlanner planner,
    IAudioArtifactRepository artifacts,
    AudioArtifactAssembler assembler,
    AudiobookPreparationCache preparations,
    IServiceProvider services)
{
    /// <summary>Returns only complete, validated finite section audio. Empty sections are rejected.</summary>
    public async Task<AudiobookSectionRenderResult> RenderAsync(AudiobookRenderRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Section.SpeechText))
            throw new ArgumentException("An empty section has no audiobook audio.", nameof(request));
        var settings = request.Settings;
        ArgumentOutOfRangeException.ThrowIfNegative(settings.ImmersivePauseMs);
        var provider = services.GetRequiredKeyedService<ITtsService>(settings.Provider);
        var emphasis = settings.EnableTextEmphasis && provider.SupportsTextEmphasis(settings.ModelId)
            ? AudioCacheKey.Create("emphasis-template-v1", provider.GetEmphasisMessages(string.Empty)) : null;
        var preparationKey = settings.EnableImmersive
            ? AudioCacheKey.ImmersivePreparation(request.Section, settings,
                new PrepareImmersiveTtsContextInfoDto { NovelId = request.NovelId, TtsModelId = settings.ModelId }.GetStructuredOutputOptions()!) : null;
        var narratorPreparation = emphasis is null ? null :
            AudioCacheKey.Emphasis(request.Section.SpeechText, settings.ToTtsOptions(), emphasis);
        var source = settings.EnableImmersive
            ? AudioCacheKey.ImmersiveSource(request.Section, settings, preparationKey!, emphasis)
            : AudioCacheKey.Source(request.Section.SpeechText, settings.ToTtsOptions(), narratorPreparation);
        var sourceKey = AudioCacheKey.Create("section-source-v1", new { source, splitter = AudiobookTextSplitter.Version });
        using var lease = await artifacts.AcquireAsync(sourceKey, cancellationToken);
        var manifest = await artifacts.ReadManifestAsync(sourceKey, cancellationToken);
        var reused = manifest is not null;
        string[] keys;
        if (manifest is not null) keys = manifest.ArtifactKeys;
        else
        {
            var planKey = settings.EnableImmersive
                ? AudioCacheKey.ImmersiveSource(request.Section, settings, preparationKey!, null) : sourceKey;
            var chunks = await preparations.GetOrCreateAsync(request.Section, "plan-" + planKey,
                () => settings.EnableImmersive
                    ? planner.PrepareAudiobookAsync(request, cancellationToken)
                    : Task.FromResult(ImmutableArray.Create(new AudiobookSpeechChunk(request.Section.SpeechText,
                        settings.VoiceId, settings.NarratorVoiceRevision, false))));
            if (chunks.IsEmpty) throw new InvalidDataException("The section plan contains no speech.");
            var completed = new List<string>();
            foreach (var chunk in chunks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                completed.Add(await tts.GenerateArtifactAsync(new TextToSpeechGenerationRequest
                {
                    Message = chunk.Text,
                    ResolvedOptions = settings.ToTtsOptions(chunk.VoiceId, chunk.VoiceRevision),
                    PreparationScope = request.Section
                }, cancellationToken));
            }
            keys = completed.ToArray();
            // Publish the durable planner-to-audio mapping only after every speech unit completed.
            await artifacts.SaveManifestAsync(new(1, sourceKey, preparationKey ?? narratorPreparation, keys), cancellationToken);
        }
        var artifact = await assembler.AssembleAsync(keys, settings.EnableImmersive ? settings.ImmersivePauseMs : 0, cancellationToken);
        return new(artifact, keys.ToImmutableArray(), reused);
    }
}
