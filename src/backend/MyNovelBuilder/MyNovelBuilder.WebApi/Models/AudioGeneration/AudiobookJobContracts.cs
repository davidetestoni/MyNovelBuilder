namespace MyNovelBuilder.WebApi.Models.AudioGeneration;

/// <summary>Submits an unexpired in-memory preview for durable background execution.</summary>
/// <param name="SnapshotId">Identifier returned by preview for the same novel. Starting uses those exact frozen inputs.</param>
/// <param name="IdempotencyKey">Client key unique within the novel. Repeating it with the same snapshot returns the existing job.</param>
public sealed record StartAudiobookJobRequest(Guid SnapshotId, string IdempotencyKey);
/// <summary>Free preview of saved-novel generation scope and effective settings. Cache estimates are rechecked during execution.</summary>
/// <param name="SnapshotId">Identifier of the frozen snapshot held in application memory.</param>
/// <param name="ExpiresAt">Time after which an unsubmitted preview must be refreshed.</param>
/// <param name="SnapshotAt">Time when saved content and generation inputs were captured.</param>
/// <param name="Settings">Explicit, effective non-secret setting selections for this export; global defaults are unchanged.</param>
/// <param name="EligibleSections">Number of sections with non-empty normalized speech.</param>
/// <param name="SkippedSections">Number of sections with no normalized speech.</param>
/// <param name="SkippedChapters">Number of chapters with no eligible sections, including chapters with no sections.</param>
/// <param name="ReusableSections">Eligible sections with validated source-to-audio mappings at preview time; assembled audio may still need rebuilding.</param>
/// <param name="CharactersNeedingSynthesis">UTF-16 code-unit count of normalized source text in sections lacking complete cache mappings. This is a source-text estimate, not an exact provider workload, price or duration.</param>
/// <param name="Warnings">User-facing scope, billing, retry and capability notices.</param>
public sealed record AudiobookPreview(Guid SnapshotId, DateTimeOffset ExpiresAt, DateTimeOffset SnapshotAt,
    AudiobookSettingsRequest Settings, int EligibleSections, int SkippedSections, int SkippedChapters,
    int ReusableSections, long CharactersNeedingSynthesis, string[] Warnings);
/// <summary>Progress of one section in the current attempt, without source text or internal artifact references.</summary>
/// <param name="ChapterIndex">Zero-based chapter position in the frozen snapshot.</param>
/// <param name="SectionIndex">Zero-based section position within the frozen chapter.</param>
/// <param name="State">Section state: pending, running, skipped, generated, reused or failed.</param>
public sealed record AudiobookSectionStatus(int ChapterIndex, int SectionIndex, string State);
/// <summary>Public history entry for one execution attempt; retries create new entries.</summary>
/// <param name="Number">One-based attempt number within the job.</param>
/// <param name="State">Attempt state: queued, running, cancelling, cancelled, failed or completed.</param>
/// <param name="Stage">Current or terminal stage. sections_ready means section rendering finished; it does not imply chapter or ZIP publication.</param>
/// <param name="ErrorCode">Sanitized failure code, or null; excludes provider response bodies and raw exception messages.</param>
/// <param name="CreatedAt">UTC time when this attempt was queued.</param>
/// <param name="FinishedAt">UTC terminal time, or null while the attempt is active.</param>
public sealed record AudiobookAttemptStatus(int Number, string State, string Stage, string? ErrorCode,
    DateTime CreatedAt, DateTime? FinishedAt);
/// <summary>Reconnectable job status. Progress counts describe the current attempt, while attempt history is retained.</summary>
/// <param name="Id">Identifier of the job.</param>
/// <param name="NovelId">Identifier of the novel that owns the job.</param>
/// <param name="SnapshotAt">UTC capture time of the original frozen inputs, preserved across retries.</param>
/// <param name="State">Job state: queued, running, cancelling, cancelled, failed or completed.</param>
/// <param name="Stage">Current attempt stage. sections_ready currently marks successful rendering before chapter packaging is implemented.</param>
/// <param name="AttemptNumber">One-based number of the current attempt.</param>
/// <param name="CompletedSections">Number of generated plus reused sections in the current attempt; excludes skipped sections.</param>
/// <param name="ReusedSections">Sections completed using validated source-to-audio mappings; missing assembled files may have been rebuilt.</param>
/// <param name="GeneratedSections">Sections completed without a prior valid source mapping. Some or all synthesis chunks may still have been reused.</param>
/// <param name="FailedSections">Sections marked failed in the current attempt; excludes pending or cancelled work.</param>
/// <param name="SkippedSections">Sections omitted because their normalized speech is empty.</param>
/// <param name="CompletedChapters">Chapters whose eligible sections are all complete. This counts rendering progress, not published chapter files.</param>
/// <param name="TotalChapters">Number of chapters containing at least one eligible section.</param>
/// <param name="DownloadAvailable">Whether a complete archive can be downloaded. Always false until chapter and ZIP publication is implemented.</param>
/// <param name="ErrorCode">Sanitized error code for the current attempt, or null.</param>
/// <param name="Sections">Current attempt section statuses ordered by chapter and section position, including skipped sections.</param>
/// <param name="Attempts">Attempt history ordered by one-based attempt number.</param>
public sealed record AudiobookJobStatus(Guid Id, Guid NovelId, DateTime SnapshotAt, string State, string Stage,
    int AttemptNumber, int CompletedSections, int ReusedSections, int GeneratedSections, int FailedSections,
    int SkippedSections, int CompletedChapters, int TotalChapters, bool DownloadAvailable, string? ErrorCode,
    IReadOnlyList<AudiobookSectionStatus> Sections, IReadOnlyList<AudiobookAttemptStatus> Attempts);
