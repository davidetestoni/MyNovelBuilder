namespace MyNovelBuilder.WebApi.Models.AudioGeneration;

/// <summary>One private JSON job file. Contains hashes and progress, never prose, prompts or credentials.</summary>
public sealed class AudiobookJob
{
    /// <summary>Metadata schema version.</summary>
    public int Version { get; set; } = 1;
    /// <summary>Job identifier, used as the filename.</summary>
    public Guid Id { get; set; }
    /// <summary>Owning novel, used as the containing directory.</summary>
    public Guid NovelId { get; set; }
    /// <summary>Original preview identifier; text-bearing snapshots live only in memory.</summary>
    public Guid SnapshotId { get; set; }
    /// <summary>Client submission key, unique within this novel.</summary>
    public string IdempotencyKey { get; set; } = "";
    /// <summary>Versioned hash of frozen metadata, settings and ordered section inputs for restart verification.</summary>
    public string InputChecksum { get; set; } = "";
    /// <summary>Explicit non-secret setting selections for reconstruction after restart.</summary>
    public AudiobookSettingsRequest Settings { get; set; } = new();
    /// <summary>UTC capture time of the original snapshot.</summary>
    public DateTime SnapshotAt { get; set; }
    /// <summary>UTC submission time of this job.</summary>
    public DateTime CreatedAt { get; set; }
    /// <summary>queued, running, cancelling, cancelled, failed or completed.</summary>
    public string State { get; set; } = "queued";
    /// <summary>Current stage; sections_ready means rendering finished, not that a ZIP is available.</summary>
    public string Stage { get; set; } = "queued";
    /// <summary>One-based execution count, incremented for explicit retries.</summary>
    public int AttemptNumber { get; set; } = 1;
    /// <summary>UTC time when the current attempt was queued.</summary>
    public DateTime AttemptCreatedAt { get; set; }
    /// <summary>UTC finish time, or null while the current attempt is active.</summary>
    public DateTime? FinishedAt { get; set; }
    /// <summary>Fixed failure code; never a raw exception or provider response.</summary>
    public string? ErrorCode { get; set; }
    /// <summary>Current section progress. Audio reuse is determined by the shared hash cache, not this list.</summary>
    public List<AudiobookSectionProgress> Sections { get; set; } = [];
    /// <summary>Small summaries of prior attempts, without duplicated section checkpoints.</summary>
    public List<AudiobookAttemptStatus> History { get; set; } = [];
}

/// <summary>Position and audio reference for a section; no separate IDs or database relationships.</summary>
public sealed class AudiobookSectionProgress
{
    /// <summary>Zero-based chapter position in the frozen snapshot.</summary>
    public int ChapterIndex { get; set; }
    /// <summary>Zero-based section position within the frozen chapter.</summary>
    public int SectionIndex { get; set; }
    /// <summary>pending, running, skipped, generated, reused or failed.</summary>
    public string State { get; set; } = "pending";
    /// <summary>Hash of completed section audio, or null. Always validate cached audio before reuse.</summary>
    public string? ArtifactKey { get; set; }
}
