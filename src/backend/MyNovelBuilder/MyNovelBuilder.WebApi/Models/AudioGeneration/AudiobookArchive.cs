namespace MyNovelBuilder.WebApi.Models.AudioGeneration;

/// <summary>Private ZIP identity and integrity, without an absolute filesystem path.</summary>
/// <param name="Key">Versioned hash of the exported manifest.</param>
/// <param name="Sha256">Checksum of the complete ZIP bytes.</param>
/// <param name="Length">Complete ZIP size in bytes.</param>
/// <param name="FileName">Safe suggested download filename.</param>
public sealed record AudiobookArchiveReference(string Key, string Sha256, long Length, string FileName);

/// <summary>Public export metadata. Contains titles and settings, but no prose, prompts or endpoint URLs.</summary>
/// <param name="Version">Manifest schema version.</param>
/// <param name="Title">Frozen novel title.</param>
/// <param name="Author">Frozen author name.</param>
/// <param name="SnapshotAt">Time the source inputs were captured.</param>
/// <param name="Settings">Effective non-secret generation selections.</param>
/// <param name="OutputProfile">PCM format used by all chapters.</param>
/// <param name="InterSectionGapMs">Silence between non-empty sections, never after the last section.</param>
/// <param name="Chapters">Non-empty chapters in original novel order.</param>
/// <param name="SkippedChapters">Zero-based positions of omitted empty chapters.</param>
/// <param name="SkippedSections">Positions of omitted empty sections.</param>
/// <param name="Warnings">Explanations of omitted content.</param>
public sealed record AudiobookArchiveManifest(int Version, string Title, string Author, DateTimeOffset SnapshotAt,
    AudiobookSettingsRequest Settings, string OutputProfile, int InterSectionGapMs,
    AudiobookArchiveChapter[] Chapters, int[] SkippedChapters, AudiobookSkippedSection[] SkippedSections, string[] Warnings);

/// <summary>One complete chapter file and its section timing information.</summary>
/// <param name="ChapterIndex">Original zero-based chapter position.</param>
/// <param name="Title">Frozen chapter title.</param>
/// <param name="FileName">Unique, safe WAV entry name.</param>
/// <param name="ArtifactKey">Hash-addressed chapter audio reference.</param>
/// <param name="Sha256">Checksum of the chapter WAV bytes.</param>
/// <param name="DurationSeconds">Chapter duration including inter-section gaps.</param>
/// <param name="Sections">Spoken sections in playback order.</param>
public sealed record AudiobookArchiveChapter(int ChapterIndex, string Title, string FileName, string ArtifactKey,
    string Sha256, double DurationSeconds, AudiobookArchiveSection[] Sections);

/// <summary>Timing of one spoken section within a chapter.</summary>
/// <param name="SectionIndex">Original zero-based section position.</param>
/// <param name="OffsetSeconds">Start offset from the beginning of the chapter.</param>
/// <param name="DurationSeconds">Section duration, excluding the following chapter gap.</param>
public sealed record AudiobookArchiveSection(int SectionIndex, double OffsetSeconds, double DurationSeconds);

/// <summary>Position of an empty section omitted from the audio.</summary>
/// <param name="ChapterIndex">Original zero-based chapter position.</param>
/// <param name="SectionIndex">Original zero-based section position.</param>
public sealed record AudiobookSkippedSection(int ChapterIndex, int SectionIndex);
