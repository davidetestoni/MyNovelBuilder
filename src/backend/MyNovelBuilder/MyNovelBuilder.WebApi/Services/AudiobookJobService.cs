using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Models.AudioGeneration;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>Job lifecycle over JSON metadata and the shared hash-addressed audio cache. Story snapshots stay in memory.</summary>
public sealed class AudiobookJobService(FileSystemAudiobookJobRepository jobs, INovelService novels, AudiobookSnapshotBuilder builder,
    AudiobookSectionRenderer renderer, AudiobookSnapshotStore store, TimeProvider clock, AudiobookArchiveService archives)
{
    /// <summary>Freezes saved inputs and estimates uncached source characters without calling generation providers.</summary>
    public async Task<AudiobookPreview> PreviewAsync(Guid novelId, AudiobookSettingsRequest request, CancellationToken ct)
    {
        var snapshot = await builder.BuildAsync(novelId, request, ct);
        var sections = snapshot.Chapters.SelectMany(c => c.Sections.Select(s => new AudiobookRenderRequest(novelId, c.ChapterIndex, snapshot.Settings, s))).ToArray();
        var eligible = sections.Where(s => !string.IsNullOrWhiteSpace(s.Section.SpeechText)).ToArray();
        if (eligible.Length == 0) throw Bad("The novel has no spoken prose. Add prose before generating an audiobook.");
        int reused = 0;
        long characters = 0;
        foreach (var section in eligible)
        {
            if (await renderer.IsReusableAsync(section, ct)) reused++;
            else characters += section.Section.SpeechText.Length;
        }
        await store.Mutations.WaitAsync(ct);
        try
        {
            await RequireNovelAsync(novelId, ct);
            var id = store.Add(snapshot);
            return new(id, clock.GetUtcNow().AddMinutes(15), snapshot.CapturedAt, ToRequest(snapshot.Settings),
                eligible.Length, sections.Length - eligible.Length,
                snapshot.Chapters.Count(c => c.Sections.All(s => string.IsNullOrWhiteSpace(s.SpeechText))), reused, characters,
                ["Generation may incur provider charges. Character counts are source-text estimates, not prices or durations.",
                 "Unassigned immersive character voices use the narrator.",
                 "After a restart, incomplete sections may require paid preprocessing again.",
                 "Downloads contain uncompressed PCM WAV chapters and may be large."]);
        }
        finally { store.Mutations.Release(); }
    }

    /// <summary>Queues a valid preview and pins its inputs. Repeated submissions with the same key return the existing job.</summary>
    public async Task<AudiobookJobStatus> StartAsync(Guid novelId, StartAudiobookJobRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 128) throw Bad("An idempotency key of 1–128 characters is required.");
        await store.Mutations.WaitAsync(ct);
        try
        {
            await RequireNovelAsync(novelId, ct);
            var existing = (await jobs.ListAsync(novelId, ct)).SingleOrDefault(j => j.IdempotencyKey == request.IdempotencyKey);
            if (existing is not null)
            {
                if (existing.SnapshotId != request.SnapshotId) throw Bad("This idempotency key belongs to a different snapshot.");
                return Status(existing);
            }
            await RequireIdleAsync(novelId, ct);
            var snapshot = store.Get(request.SnapshotId, novelId) ?? throw Bad("The preview expired. Create a new preview.");
            var job = new AudiobookJob
            {
                Id = Guid.NewGuid(), NovelId = novelId, SnapshotId = request.SnapshotId,
                IdempotencyKey = request.IdempotencyKey, InputChecksum = Checksum(snapshot),
                Settings = ToRequest(snapshot.Settings),
                SnapshotAt = snapshot.CapturedAt.UtcDateTime, CreatedAt = clock.GetUtcNow().UtcDateTime
            };
            ResetProgress(job, snapshot);
            await jobs.SaveAsync(job, ct);
            store.Restore(job.SnapshotId, snapshot);
            return Status(job);
        }
        finally { store.Mutations.Release(); }
    }

    /// <summary>Queues failed, cancelled or unavailable completed work again; after restart, saved inputs must match the original checksum.</summary>
    public async Task<AudiobookJobStatus> RetryAsync(Guid novelId, Guid jobId, CancellationToken ct)
    {
        await store.Mutations.WaitAsync(ct);
        try
        {
            var job = await FindAsync(novelId, jobId, ct);
            if (job.State == "completed")
            {
                await using var existing = await archives.OpenAsync(novelId, job.Archive, ct);
                if (existing is not null) throw Bad("This audiobook is already complete and available for download.");
            }
            else if (job.State is not ("failed" or "cancelled")) throw Bad("Only failed, cancelled or unavailable completed jobs can be retried.");
            await RequireIdleAsync(novelId, ct);
            var snapshot = store.Get(job.SnapshotId, novelId);
            if (snapshot is null)
            {
                snapshot = await builder.BuildAsync(novelId, job.Settings, ct);
                if (Checksum(snapshot) != job.InputChecksum) throw Bad("Saved inputs changed since this snapshot. Create a new preview.");
                snapshot = snapshot with { CapturedAt = new DateTimeOffset(job.SnapshotAt, TimeSpan.Zero) };
            }
            job.History.Add(AttemptStatus(job));
            job.AttemptNumber++;
            ResetProgress(job, snapshot);
            await jobs.SaveAsync(job, ct);
            store.Restore(job.SnapshotId, snapshot);
            return Status(job);
        }
        finally { store.Mutations.Release(); }
    }

    /// <summary>Cancels queued work immediately or signals running work to stop, preserving completed cached audio.</summary>
    public async Task<AudiobookJobStatus> CancelAsync(Guid novelId, Guid jobId, CancellationToken ct)
    {
        await store.Mutations.WaitAsync(ct);
        try
        {
            var job = await FindAsync(novelId, jobId, ct);
            if (job.State is "queued" or "running" or "cancelling")
            {
                job.State = job.State == "queued" ? "cancelled" : "cancelling";
                if (job.State == "cancelled") { job.FinishedAt = clock.GetUtcNow().UtcDateTime; job.Stage = "cancelled"; }
                await jobs.SaveAsync(job, ct);
                store.Cancel(job.Id);
            }
            return Status(job);
        }
        finally { store.Mutations.Release(); }
    }
    /// <summary>Returns job progress only when the job belongs to the requested novel.</summary>
    public async Task<AudiobookJobStatus> GetAsync(Guid novelId, Guid jobId, CancellationToken ct) => Status(await FindAsync(novelId, jobId, ct));
    /// <summary>Returns up to 50 jobs for an existing novel, with active jobs first and newest jobs next.</summary>
    public async Task<IReadOnlyList<AudiobookJobStatus>> ListAsync(Guid novelId, CancellationToken ct)
    {
        await RequireNovelAsync(novelId, ct);
        return (await jobs.ListAsync(novelId, ct)).OrderByDescending(j => j.State is "queued" or "running" or "cancelling")
            .ThenByDescending(j => j.CreatedAt).Take(50).Select(Status).ToArray();
    }
    /// <summary>Returns a validated ZIP stream for a completed job belonging to the novel. The caller must dispose the stream.</summary>
    public async Task<(Stream Stream, string FileName)> DownloadAsync(Guid novelId, Guid jobId, CancellationToken ct)
    {
        var job = await FindAsync(novelId, jobId, ct);
        if (job.State != "completed") throw Bad("This audiobook is not ready for download.");
        var stream = await archives.OpenAsync(novelId, job.Archive, ct)
            ?? throw Bad("The audiobook archive is missing or damaged. Retry the job to rebuild it from retained audio.");
        return (stream, job.Archive!.FileName);
    }

    private async Task<AudiobookJob> FindAsync(Guid novelId, Guid id, CancellationToken ct) =>
        await jobs.ReadAsync(novelId, id, ct) ?? throw Bad("Audiobook job not found for this novel.");
    private async Task RequireNovelAsync(Guid id, CancellationToken ct) => await novels.GetByIdAsync(id, ct);
    private async Task RequireIdleAsync(Guid novelId, CancellationToken ct)
    {
        if ((await jobs.ListAsync(novelId, ct)).Any(j => j.State is "queued" or "running" or "cancelling"))
            throw Bad("This novel already has an active audiobook job.");
    }
    /// <summary>Hashes frozen metadata, settings and ordered section identities, excluding the snapshot capture time.</summary>
    public string Checksum(AudiobookSnapshot snapshot) => AudioCacheKey.Create("audiobook-snapshot-v1", new
    {
        snapshot.NovelId, snapshot.Title, snapshot.Author, snapshot.Settings,
        chapters = snapshot.Chapters.Select(c => new { c.ChapterIndex, c.Title,
            sections = c.Sections.Select(s => new { s.SectionIndex, key = renderer.GetIdentity(new(snapshot.NovelId, c.ChapterIndex, snapshot.Settings, s)).SourceKey }) })
    });
    private void ResetProgress(AudiobookJob job, AudiobookSnapshot snapshot)
    {
        job.State = job.Stage = "queued";
        job.AttemptCreatedAt = clock.GetUtcNow().UtcDateTime;
        job.FinishedAt = null;
        job.ErrorCode = null;
        job.Archive = null;
        job.CompletedChapters = 0;
        job.Sections = snapshot.Chapters.SelectMany(c => c.Sections.Select(s => new AudiobookSectionProgress
        {
            ChapterIndex = c.ChapterIndex, SectionIndex = s.SectionIndex,
            State = string.IsNullOrWhiteSpace(s.SpeechText) ? "skipped" : "pending"
        })).ToList();
    }
    private static AudiobookAttemptStatus AttemptStatus(AudiobookJob job) =>
        new(job.AttemptNumber, job.State, job.Stage, job.ErrorCode, job.AttemptCreatedAt, job.FinishedAt);
    /// <summary>Copies resolved settings into explicit selections that can reconstruct inputs after restart.</summary>
    public static AudiobookSettingsRequest ToRequest(ResolvedAudiobookSettings s) => new(s.Provider, s.ModelId, s.VoiceId,
        s.EnableTextEmphasis, s.EnableImmersive, s.ImmersivePauseMs, s.TextGenerationProvider, s.TextGenerationModelId, s.ImmersivePromptId);
    /// <summary>Projects saved progress and attempt summaries, checking whether the published archive is still present.</summary>
    public AudiobookJobStatus Status(AudiobookJob job)
    {
        var sections = job.Sections.OrderBy(s => s.ChapterIndex).ThenBy(s => s.SectionIndex).ToArray();
        var chapters = sections.Where(s => s.State != "skipped").GroupBy(s => s.ChapterIndex).ToArray();
        return new(job.Id, job.NovelId, job.SnapshotAt, job.State, job.Stage, job.AttemptNumber,
            sections.Count(s => s.State is "generated" or "reused"), sections.Count(s => s.State == "reused"),
            sections.Count(s => s.State == "generated"), sections.Count(s => s.State == "failed"), sections.Count(s => s.State == "skipped"),
            job.CompletedChapters, chapters.Length, job.State == "completed" && archives.IsAvailable(job.NovelId, job.Archive), job.ErrorCode,
            sections.Select(s => new AudiobookSectionStatus(s.ChapterIndex, s.SectionIndex, s.State)).ToArray(),
            job.History.Append(AttemptStatus(job)).ToArray());
    }
    private static ApiException Bad(string message) => new(ErrorCodes.BadRequest, message);
}
