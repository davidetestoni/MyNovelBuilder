using System.Collections.Immutable;
using System.Net;
using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Helpers;
using MyNovelBuilder.WebApi.Models.AudioGeneration;
using MyNovelBuilder.WebApi.Models.Novels;
using MyNovelBuilder.WebApi.Prompts.Builders;
using MyNovelBuilder.WebApi.Services.Tts;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>Captures saved prose and every rendered immersive prompt before a job starts.</summary>
public sealed class AudiobookSnapshotBuilder(
    INovelService novels,
    IUnitOfWork unitOfWork,
    AudiobookSettingsResolver settingsResolver,
    ITtsVoiceRevisionService voiceRevisions,
    IServiceProvider services)
{
    public async Task<AudiobookSnapshot> BuildAsync(
        Guid novelId,
        AudiobookSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var settings = await settingsResolver.ResolveAsync(request, cancellationToken);
        var novel = await novels.GetByIdAsync(novelId, cancellationToken);
        var prose = await novels.GetProseAsync(novelId, cancellationToken);
        Prompt? prompt = null;
        var records = new List<CompendiumRecord>();
        if (settings.EnableImmersive)
        {
            prompt = await unitOfWork.Prompts.GetWithMessagesByIdAsync(
                settings.ImmersivePromptId!.Value, cancellationToken);
            if (prompt is null || prompt.Type != PromptType.PrepareImmersiveTts)
            {
                throw new ApiException(ErrorCodes.BadRequest, "The selected immersive prompt is unavailable.");
            }
            foreach (var compendium in novel.Compendia)
            {
                records.AddRange(await unitOfWork.CompendiumRecords.GetByCompendiumIdAsync(
                    compendium.Id, cancellationToken));
            }
        }

        var initialSnapshot = Capture(novel, prose, settings, prompt, records);
        var recordedVoiceRevisions = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var voiceId in initialSnapshot.Chapters.SelectMany(chapter => chapter.Sections)
                     .SelectMany(section => section.VoiceAssignments)
                     .Select(assignment => assignment.VoiceId).Distinct(StringComparer.Ordinal))
        {
            recordedVoiceRevisions[voiceId] = await voiceRevisions.GetRevisionAsync(
                settings.Provider, voiceId, cancellationToken);
        }

        var snapshot = initialSnapshot with
        {
            Chapters = initialSnapshot.Chapters.Select(chapter => chapter with
            {
                Sections = chapter.Sections.Select(section => section with
                {
                    VoiceAssignments = section.VoiceAssignments.Select(assignment => assignment with
                    {
                        VoiceRevision = recordedVoiceRevisions[assignment.VoiceId]
                    }).ToImmutableArray()
                }).ToImmutableArray()
            }).ToImmutableArray()
        };
        if (settings.EnableImmersive)
        {
            var tts = services.GetRequiredKeyedService<ITtsService>(settings.Provider);
            var model = (await tts.GetModelsAsync(cancellationToken))
                .FirstOrDefault(m => m.ModelId == settings.ModelId);
            if (model is null)
            {
                throw new ApiException(ErrorCodes.BadRequest,
                    $"TTS model '{settings.ModelId}' became unavailable while preparing the audiobook.");
            }
            var availableVoiceIds = model.Voices.Select(voice => voice.VoiceId)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var assignment in snapshot.Chapters.SelectMany(chapter => chapter.Sections)
                         .SelectMany(section => section.VoiceAssignments))
            {
                if (!availableVoiceIds.Contains(assignment.VoiceId))
                {
                    throw new ApiException(ErrorCodes.BadRequest,
                        $"Character voice '{assignment.VoiceId}' is not available for model '{settings.ModelId}'.");
                }
            }
        }
        return snapshot;
    }

    /// <summary>Copies mutable source objects into detached, immutable generation inputs.</summary>
    public static AudiobookSnapshot Capture(
        Novel novel,
        Prose prose,
        ResolvedAudiobookSettings settings,
        Prompt? prompt = null,
        IReadOnlyList<CompendiumRecord>? records = null)
    {
        records ??= [];
        // Existing prompt builders expect HTML and decode it once. Encode normalized
        // speech so literal spoken '<'/'&' survive that pass in every context placeholder.
        var promptProse = new Prose
        {
            Chapters = prose.Chapters.Select(chapter => new Chapter
            {
                Title = chapter.Title,
                StoryEvents = chapter.StoryEvents,
                Sections = chapter.Sections.Select(section => new Section
                {
                    Text = WebUtility.HtmlEncode(AudiobookSpeechTextNormalizer.Normalize(section.Text)),
                    Summary = section.Summary,
                    Images = section.Images,
                    RecordOverrides = section.RecordOverrides
                }).ToList()
            }).ToList()
        };
        var chapters = prose.Chapters.Select((chapter, chapterIndex) =>
            new AudiobookChapterSnapshot(
                chapterIndex,
                chapter.Title,
                chapter.Sections.Select((section, sectionIndex) =>
                {
                    var speechText = WebUtility.HtmlDecode(promptProse.Chapters[chapterIndex].Sections[sectionIndex].Text);
                    if (prompt is null || speechText.Length == 0)
                    {
                        return new AudiobookSectionSnapshot(
                            sectionIndex, speechText, [], []);
                    }

                    var includedIds = new HashSet<Guid>();
                    var context = new NovelPromptBuilderContext<PrepareImmersiveTtsContextInfoDto>
                    {
                        Client = new PrepareImmersiveTtsContextInfoDto
                        {
                            NovelId = novel.Id,
                            ChapterIndex = chapterIndex,
                            SectionIndex = sectionIndex,
                            Provider = settings.Provider,
                            TtsModelId = settings.ModelId
                        },
                        Novel = novel,
                        Prose = promptProse,
                        CompendiumRecords = records.ToList(),
                        IncludedCompendiumRecordIds = includedIds
                    };
                    var messages = prompt.Messages.Select(message => new AudiobookPromptMessage(
                        message.Role,
                        new PrepareImmersiveTtsPromptBuilder(message.Message)
                            .ReplacePlaceholders(context).ToString())).ToImmutableArray();
                    var voices = records.Where(record => includedIds.Contains(record.Id))
                        .SelectMany(record => record.CharacterVoiceAssignments
                            .Where(assignment => assignment.Provider == settings.Provider
                                && assignment.ModelId == settings.ModelId)
                            .Select(assignment => new AudiobookVoiceAssignment(
                                record.Id, assignment.VoiceId, assignment.UpdatedAt,
                                null)))
                        .OrderBy(assignment => assignment.CharacterRecordId)
                        .ThenBy(assignment => assignment.VoiceId, StringComparer.Ordinal)
                        .ToImmutableArray();
                    return new AudiobookSectionSnapshot(sectionIndex, speechText, messages, voices);
                }).ToImmutableArray())).ToImmutableArray();

        return new AudiobookSnapshot(novel.Id, novel.Title, novel.Author,
            DateTimeOffset.UtcNow, settings, chapters);
    }
}
