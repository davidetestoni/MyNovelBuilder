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

/// <summary>Frozen chapter content and its ordered speech sections.</summary>
/// <param name="ChapterIndex">Chapter position in the novel.</param>
/// <param name="Title">Chapter title.</param>
/// <param name="Sections">Ordered sections captured for generation.</param>
public sealed record AudiobookChapterSnapshot(
    int ChapterIndex,
    string Title,
    ImmutableArray<AudiobookSectionSnapshot> Sections);

/// <summary>Frozen text, prompts and voice assignments for one section.</summary>
/// <param name="SectionIndex">Section position in the chapter.</param>
/// <param name="SpeechText">Exact source speech text.</param>
/// <param name="ImmersivePrompt">Captured immersive planning prompt.</param>
/// <param name="VoiceAssignments">Captured character voice selections.</param>
public sealed record AudiobookSectionSnapshot(
    int SectionIndex,
    string SpeechText,
    ImmutableArray<AudiobookPromptMessage> ImmersivePrompt,
    ImmutableArray<AudiobookVoiceAssignment> VoiceAssignments);

/// <summary>One captured prompt message.</summary>
/// <param name="Role">Message role.</param>
/// <param name="Message">Rendered message text.</param>
public sealed record AudiobookPromptMessage(PromptMessageRole Role, string Message);

/// <summary>Frozen character voice metadata used in audio identities.</summary>
/// <param name="CharacterRecordId">Character record identifier.</param>
/// <param name="VoiceId">Provider voice identifier.</param>
/// <param name="UpdatedAt">Voice assignment update time.</param>
/// <param name="VoiceRevision">Recorded sample fingerprint.</param>
/// <param name="GainDb">Fixed playback gain in decibels.</param>
public sealed record AudiobookVoiceAssignment(
    Guid CharacterRecordId,
    string VoiceId,
    DateTime UpdatedAt,
    string? VoiceRevision, double GainDb = 0);
