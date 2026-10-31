using Microsoft.Extensions.Options;
using MyNovelBuilder.WebApi.Models.AudioGeneration;
using MyNovelBuilder.WebApi.Options;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>One background consumer of filesystem job metadata. Never resumes paid calls on startup.</summary>
public sealed class AudiobookWorker(IServiceScopeFactory scopes, AudiobookSnapshotStore store,
    IOptions<AppStorageOptions> storage, ILogger<AudiobookWorker> logger) : BackgroundService
{
    private FileStream? workerLease;
    /// <summary>Acquires exclusive ownership of the local queue and marks interrupted work before starting consumption.</summary>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        var folder = Path.Combine(storage.Value.DataFolder, "audiobooks");
        Directory.CreateDirectory(folder);
        // One host owns this queue. OS file leases are released even after a crash.
        workerLease = new FileStream(Path.Combine(folder, "worker.lock"), FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None);
        try
        {
            await RecoverAsync(cancellationToken);
            await store.Mutations.WaitAsync(cancellationToken);
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<GeneratedAudioCleanup>().RunAsync(cancellationToken);
            }
            finally { store.Mutations.Release(); }
            await base.StartAsync(cancellationToken);
        }
        catch { await workerLease.DisposeAsync(); workerLease = null; throw; }
    }
    /// <summary>Disposes the background service and releases the queue’s file lease.</summary>
    public override void Dispose()
    {
        base.Dispose();
        workerLease?.Dispose();
    }
    /// <summary>Processes jobs sequentially and prunes expired previews, polling once per second when idle.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            store.PruneExpired();
            if (!await RunNextAsync(stoppingToken)) await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    /// <summary>Marks unfinished saved jobs as interrupted so generation requires an explicit, verified retry.</summary>
    public async Task RecoverAsync(CancellationToken ct)
    {
        await store.Mutations.WaitAsync(ct);
        try
        {
            using var scope = scopes.CreateScope();
            var jobs = scope.ServiceProvider.GetRequiredService<FileSystemAudiobookJobRepository>();
            foreach (var job in (await jobs.ListAsync(ct: ct)).Where(j => j.State is "queued" or "running" or "cancelling"))
            {
                job.State = "failed";
                job.Stage = "interrupted";
                job.ErrorCode = "interrupted_requires_verified_retry";
                job.FinishedAt = DateTime.UtcNow;
                foreach (var section in job.Sections.Where(s => s.State == "running")) section.State = "pending";
                await jobs.SaveAsync(job, ct);
            }
        }
        finally { store.Mutations.Release(); }
    }

    /// <summary>Claims the oldest queued job and renders its sections with persisted progress and cancellation checks.</summary>
    /// <returns>True if a job was claimed, including when it failed or was cancelled; false if the queue was empty.</returns>
    public async Task<bool> RunNextAsync(CancellationToken stoppingToken)
    {
        using var scope = scopes.CreateScope();
        var jobs = scope.ServiceProvider.GetRequiredService<FileSystemAudiobookJobRepository>();
        var renderer = scope.ServiceProvider.GetRequiredService<AudiobookSectionRenderer>();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        AudiobookJob? job;
        AudiobookSnapshot? snapshot;
        await store.Mutations.WaitAsync(stoppingToken);
        try
        {
            job = (await jobs.ListAsync(ct: stoppingToken)).Where(j => j.State == "queued")
                .OrderBy(j => j.CreatedAt).FirstOrDefault();
            if (job is null) return false;
            job.State = "running";
            // The process gate covers read/claim/write; the worker's file lease excludes another host.
            await jobs.SaveAsync(job, CancellationToken.None);
            snapshot = store.Get(job.SnapshotId, job.NovelId);
            store.Register(job.Id, job.NovelId, cancellation);
        }
        finally { store.Mutations.Release(); }
        AudiobookSectionProgress? active = null;
        try
        {
            if (snapshot is null) throw new SnapshotUnavailableException();
            foreach (var section in job.Sections.OrderBy(s => s.ChapterIndex).ThenBy(s => s.SectionIndex))
            {
                cancellation.Token.ThrowIfCancellationRequested();
                if (section.State == "skipped") continue;
                active = section;
                section.State = "running";
                await CheckpointAsync("planning");
                var chapter = snapshot.Chapters.Single(c => c.ChapterIndex == section.ChapterIndex);
                var input = chapter.Sections.Single(s => s.SectionIndex == section.SectionIndex);
                var result = await ProviderRetry.ExecuteAsync(
                    () => renderer.RenderAsync(new(job.NovelId, chapter.ChapterIndex, snapshot.Settings, input),
                        cancellation.Token, CheckpointAsync), cancellation.Token,
                    onRetry: (attempt, delay, error) => logger.LogWarning(error,
                        "Audiobook job {JobId}, chapter {Chapter}, section {Section}, stage {Stage}, "
                        + "provider {Provider}, model {ModelId}: attempt {Attempt}/3 failed; retrying in {DelaySeconds}s.",
                        job.Id, section.ChapterIndex + 1, section.SectionIndex + 1, job.Stage,
                        snapshot.Settings.Provider, snapshot.Settings.ModelId, attempt, delay.TotalSeconds));
                section.ArtifactKey = result.ArtifactKey;
                section.State = result.Reused ? "reused" : "generated";
                await CheckpointAsync("rendering");
                active = null;
            }
            var archives = scope.ServiceProvider.GetRequiredService<AudiobookArchiveService>();
            job.Archive = await archives.BuildAsync(snapshot, job.Sections, async (stage, count) =>
            {
                job.CompletedChapters = count;
                await CheckpointAsync(stage);
            }, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            await FinishAsync("completed", "ready", null);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (active is not null && active.State == "running") active.State = "pending";
            await FinishAsync(stoppingToken.IsCancellationRequested ? "failed" : "cancelled",
                stoppingToken.IsCancellationRequested ? "interrupted" : "cancelled",
                stoppingToken.IsCancellationRequested ? "interrupted_requires_verified_retry" : null);
        }
        catch (Exception ex)
        {
            if (active is not null) active.State = "failed";
            // Keep stable codes in job records; preserve exception details in server logs.
            var code = ex switch
            {
                SnapshotUnavailableException => "snapshot_unavailable_requires_verified_retry",
                HttpRequestException => "provider_request_failed",
                AudiobookExportException export => export.Code,
                IOException io when (io.HResult & 0xffff) is 28 or 39 or 112 => "insufficient_disk_space",
                IOException => "audio_storage_or_format_failed",
                InvalidOperationException => "generation_inputs_or_plan_invalid",
                _ => "generation_failed"
            };
            logger.LogError(ex,
                "Audiobook job {JobId} failed with code {Code}; chapter {Chapter}, section {Section}, "
                + "stage {Stage}, provider {Provider}, model {ModelId}.",
                job.Id, code, active?.ChapterIndex + 1, active?.SectionIndex + 1, job.Stage,
                snapshot?.Settings.Provider, snapshot?.Settings.ModelId);
            await FinishAsync("failed", "failed", code);
        }
        finally { store.Unregister(job.Id); }
        return true;

        async Task CheckpointAsync(string stage)
        {
            await store.Mutations.WaitAsync(cancellation.Token);
            try
            {
                var state = (await jobs.ReadAsync(job.NovelId, job.Id, cancellation.Token))?.State;
                if (state is null or "cancelling") { await cancellation.CancelAsync(); cancellation.Token.ThrowIfCancellationRequested(); }
                job.Stage = stage;
                await jobs.SaveAsync(job, cancellation.Token);
            }
            finally { store.Mutations.Release(); }
        }
        async Task FinishAsync(string state, string stage, string? error)
        {
            await store.Mutations.WaitAsync(CancellationToken.None);
            try
            {
                var savedState = (await jobs.ReadAsync(job.NovelId, job.Id))?.State;
                if (savedState is null) return; // Novel deletion removed the metadata.
                if (savedState == "cancelling" && state == "completed") { state = "cancelled"; stage = "cancelled"; }
                job.State = state;
                job.Stage = stage;
                job.ErrorCode = error;
                job.FinishedAt = DateTime.UtcNow;
                await jobs.SaveAsync(job, CancellationToken.None);
            }
            finally { store.Mutations.Release(); }
        }
    }
    /// <summary>The frozen inputs for the queued audiobook are no longer available.</summary>
    public sealed class SnapshotUnavailableException : Exception;
}
