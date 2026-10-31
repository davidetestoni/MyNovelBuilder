using System.Text.Json;
using Microsoft.Extensions.Options;
using MyNovelBuilder.WebApi.Models.AudioGeneration;
using MyNovelBuilder.WebApi.Options;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>One atomically replaced JSON file per job, beside private exports. Callers serialize mutations with the snapshot store's gate.</summary>
public sealed class FileSystemAudiobookJobRepository(IOptions<AppStorageOptions> options,
    ILogger<FileSystemAudiobookJobRepository> logger)
{
    private readonly string root = Path.Combine(options.Value.DataFolder, "audiobooks");
    private string PathFor(Guid novelId, Guid jobId) => Path.Combine(root, novelId.ToString(), "jobs", jobId + ".json");

    /// <summary>Reads valid metadata, or returns null for a missing or damaged job. Temporary files are never read.</summary>
    public async Task<AudiobookJob?> ReadAsync(Guid novelId, Guid jobId, CancellationToken ct = default)
    {
        try
        {
            await using var file = new FileStream(PathFor(novelId, jobId), FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            var job = await JsonSerializer.DeserializeAsync<AudiobookJob>(file, cancellationToken: ct);
            if (job is null || job.Version != 1 || job.Id != jobId || job.NovelId != novelId ||
                job.Settings is null || job.Sections is null || job.History is null ||
                job.Sections.Any(s => s is null) || job.History.Any(a => a is null) ||
                job.InputChecksum is not { Length: 64 } || job.AttemptNumber < 1 || job.MaxParallelSections is < 1 or > 8 ||
                job.State is not ("queued" or "running" or "cancelling" or "cancelled" or "failed" or "completed"))
                throw new JsonException();
            return job;
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Ignoring invalid audiobook metadata for job {JobId} in novel {NovelId}.", jobId, novelId);
            return null;
        }
    }

    /// <summary>Lists saved jobs for a novel, or all novels for the single background worker.</summary>
    public async Task<List<AudiobookJob>> ListAsync(Guid? novelId = null, CancellationToken ct = default)
    {
        var result = new List<AudiobookJob>();
        if (!Directory.Exists(root)) return result;
        var novels = novelId is { } id ? new[] { id } : Directory.GetDirectories(root)
            .Select(Path.GetFileName).Where(n => Guid.TryParse(n, out _)).Select(n => Guid.Parse(n!)).ToArray();
        foreach (var novel in novels)
        {
            var directory = Path.Combine(root, novel.ToString(), "jobs");
            if (!Directory.Exists(directory)) continue;
            foreach (var path in Directory.GetFiles(directory, "*.json"))
            {
                ct.ThrowIfCancellationRequested();
                if (Guid.TryParse(Path.GetFileNameWithoutExtension(path), out var jobId) &&
                    await ReadAsync(novel, jobId, ct) is { } job) result.Add(job);
            }
        }
        return result;
    }

    /// <summary>Flushes a complete temporary file before atomic replacement; interrupted writes leave the previous version intact.</summary>
    public async Task SaveAsync(AudiobookJob job, CancellationToken ct = default)
    {
        var path = PathFor(job.NovelId, job.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(file, job, cancellationToken: ct);
                file.Flush(flushToDisk: true);
            }
            ct.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
        }
        finally { File.Delete(temporary); }
    }
}
