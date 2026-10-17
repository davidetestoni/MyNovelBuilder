using MyNovelBuilder.WebApi.Models.AudioGeneration;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>Process-local text-bearing snapshots. Previews expire; submitted snapshots survive retries.</summary>
public sealed class AudiobookSnapshotStore(TimeProvider clock)
{
    private readonly object sync = new();
    private readonly Dictionary<Guid, Entry> entries = [];
    private readonly Dictionary<Guid, (Guid NovelId, CancellationTokenSource Cancellation)> running = [];
    /// <summary>Serializes job file changes and novel deletion within this process. Callers must release it after use.</summary>
    public SemaphoreSlim Mutations { get; } = new(1, 1);
    /// <summary>Stores a preview for 15 minutes; rejects new previews when 32 unsubmitted snapshots remain.</summary>
    public Guid Add(AudiobookSnapshot snapshot)
    {
        lock (sync)
        {
            Prune();
            // Bound abandoned preview memory, without evicting submitted work.
            if (entries.Values.Count(e => !e.Pinned) >= 32)
                throw new InvalidOperationException("Too many audiobook previews. Wait for a preview to expire.");
            var id = Guid.NewGuid();
            entries[id] = new(snapshot, clock.GetUtcNow().AddMinutes(15), false);
            return id;
        }
    }
    /// <summary>Returns a snapshot belonging to the novel, or null if missing or expired. Pinning prevents expiry.</summary>
    public AudiobookSnapshot? Get(Guid id, Guid novelId, bool pin = false)
    {
        lock (sync)
        {
            Prune();
            if (!entries.TryGetValue(id, out var entry) || entry.Snapshot.NovelId != novelId) return null;
            if (pin) entries[id] = entry with { Pinned = true };
            return entry.Snapshot;
        }
    }
    /// <summary>Stores a submitted or verified retry snapshot without an expiry.</summary>
    public void Restore(Guid id, AudiobookSnapshot snapshot)
    {
        lock (sync) entries[id] = new(snapshot, DateTimeOffset.MaxValue, true);
    }
    /// <summary>Registers a running job for cancellation. The caller owns the cancellation source.</summary>
    public void Register(Guid jobId, Guid novelId, CancellationTokenSource cancellation)
    {
        lock (sync) running[jobId] = (novelId, cancellation);
    }
    /// <summary>Removes a completed execution from the cancellation registry without disposing its source.</summary>
    public void Unregister(Guid jobId) { lock (sync) running.Remove(jobId); }
    /// <summary>Signals cancellation if the job is currently running.</summary>
    public void Cancel(Guid jobId) { lock (sync) { if (running.TryGetValue(jobId, out var value)) value.Cancellation.Cancel(); } }
    /// <summary>Cancels the novel’s running jobs and removes its in-memory snapshots.</summary>
    public void RemoveNovel(Guid novelId)
    {
        lock (sync)
        {
            foreach (var pair in running.Values.Where(v => v.NovelId == novelId)) pair.Cancellation.Cancel();
            foreach (var id in entries.Where(e => e.Value.Snapshot.NovelId == novelId).Select(e => e.Key).ToArray()) entries.Remove(id);
        }
    }
    /// <summary>Releases expired previews while retaining submitted snapshots for retries.</summary>
    public void PruneExpired() { lock (sync) Prune(); }
    private void Prune()
    {
        foreach (var id in entries.Where(e => !e.Value.Pinned && e.Value.Expires <= clock.GetUtcNow()).Select(e => e.Key).ToArray()) entries.Remove(id);
    }
    private sealed record Entry(AudiobookSnapshot Snapshot, DateTimeOffset Expires, bool Pinned);
}
