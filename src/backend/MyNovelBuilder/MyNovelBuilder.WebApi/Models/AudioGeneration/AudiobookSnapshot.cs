using System.Collections.Immutable;
using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Models.AudioGeneration;

/// <summary>
/// Detached generation inputs held in memory only. This object contains story text and
/// rendered prompts; never serialize it to a job table or cache file. Persist hashes only.
/// </summary>
public sealed record AudiobookSnapshot(
    Guid NovelId,
    string Title,
    string Author,
    DateTimeOffset CapturedAt,
    ResolvedAudiobookSettings Settings,
    ImmutableArray<AudiobookChapterSnapshot> Chapters);

public sealed record AudiobookChapterSnapshot(
    int ChapterIndex,
    string Title,
    ImmutableArray<AudiobookSectionSnapshot> Sections);

public sealed record AudiobookSectionSnapshot(
    int SectionIndex,
    string SpeechText,
    ImmutableArray<AudiobookPromptMessage> ImmersivePrompt,
    ImmutableArray<AudiobookVoiceAssignment> VoiceAssignments);

public sealed record AudiobookPromptMessage(PromptMessageRole Role, string Message);

public sealed record AudiobookVoiceAssignment(
    Guid CharacterRecordId,
    string VoiceId,
    DateTime UpdatedAt,
    string? VoiceRevision);
