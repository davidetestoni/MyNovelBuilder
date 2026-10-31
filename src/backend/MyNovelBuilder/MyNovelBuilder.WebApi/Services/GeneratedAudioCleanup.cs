using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MyNovelBuilder.WebApi.Models.AudioGeneration;
using MyNovelBuilder.WebApi.Options;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>Startup-only age cleanup of private audio files. Keeps job history and referenced speech.</summary>
public sealed class GeneratedAudioCleanup(IOptions<AppStorageOptions> options, FileSystemAudiobookJobRepository jobs,
    TimeProvider clock, ILogger<GeneratedAudioCleanup> logger)
{
    /// <summary>Removes abandoned temporary files and expired, unreferenced artifacts before the host accepts requests.</summary>
    /// <remarks>The worker must own its exclusive file lease and hold the snapshot mutation gate.</remarks>
    public async Task RunAsync(CancellationToken ct = default)
    {
        try { await CleanAsync(ct); }
        catch (IOException ex) { logger.LogWarning(ex, "Generated audio cleanup was skipped because storage is unavailable."); }
        catch (UnauthorizedAccessException ex) { logger.LogWarning(ex, "Generated audio cleanup was skipped because storage is not writable."); }
    }

    private async Task CleanAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var root = options.Value.DataFolder;
        var cache = Path.Combine(root, "audio", "v1");
        var safeCache = IsDirectory(Path.Combine(root, "audio")) && IsDirectory(cache);
        var cacheFiles = safeCache ? Files(cache) : [];
        var work = safeCache ? Files(Path.Combine(cache, "work")) : [];
        var novels = Path.Combine(root, "audiobooks");
        var directories = IsDirectory(novels) ? Directory.GetDirectories(novels).Where(d =>
            Guid.TryParse(Path.GetFileName(d), out _) && IsDirectory(d)).ToArray() : [];
        var jobFiles = directories.SelectMany(d => Files(Path.Combine(d, "jobs"))).ToArray();
        var archives = directories.SelectMany(d => Files(Path.Combine(d, "archives"))).ToArray();
        var temporaryCutoff = clock.GetUtcNow().UtcDateTime.AddDays(-1);
        foreach (var file in cacheFiles.Concat(work).Concat(jobFiles).Concat(archives))
        {
            ct.ThrowIfCancellationRequested();
            if (IsTemporary(file.Name) && file.LastWriteTimeUtc < temporaryCutoff) Delete(file);
        }
        if (options.Value.GeneratedAudioRetentionDays == 0) return;
        var cutoff = clock.GetUtcNow().UtcDateTime.AddDays(-options.Value.GeneratedAudioRetentionDays);
        var saved = await jobs.ListAsync(ct: ct);
        // A damaged job may still own audio. Leave permanent files alone until its metadata is repaired.
        if (jobFiles.Count(f => f.Extension == ".json" && Guid.TryParse(Path.GetFileNameWithoutExtension(f.Name), out _)) != saved.Count ||
            saved.Any(j => j.Sections.Any(s => s.ArtifactKey is not null && !IsHash(s.ArtifactKey)) ||
                (j.Archive is not null && (!IsHash(j.Archive.Key) || !IsHash(j.Archive.Sha256)))))
        {
            logger.LogWarning("Keeping generated audio because some saved job metadata could not be read.");
            return;
        }
        var keepArchives = saved.Where(j => j.Archive is not null && j.State is "queued" or "running" or "cancelling")
            .Select(j => (j.NovelId, j.Archive!.Key)).ToHashSet();
        var existingArchives = archives.Where(f => f.Extension == ".zip" && IsHash(Path.GetFileNameWithoutExtension(f.Name)))
            .Select(f => (Guid.Parse(f.Directory!.Parent!.Name), Path.GetFileNameWithoutExtension(f.Name))).ToHashSet();
        foreach (var group in saved.Where(j => j.State == "completed" && j.Archive is not null).GroupBy(j => j.NovelId))
        {
            // Keep the latest successful download even when an old or failed run exists after it.
            var latest = group.OrderByDescending(j => j.FinishedAt ?? j.CreatedAt).First();
            keepArchives.Add((latest.NovelId, latest.Archive!.Key));
            var available = group.Where(j => existingArchives.Contains((j.NovelId, j.Archive!.Key)))
                .OrderByDescending(j => j.FinishedAt ?? j.CreatedAt).FirstOrDefault();
            if (available is not null) keepArchives.Add((available.NovelId, available.Archive!.Key));
        }
        var required = saved.SelectMany(j => j.Sections).Select(s => s.ArtifactKey).Where(IsHash).Select(k => k!).ToHashSet();
        foreach (var file in archives.Where(f => f.Extension == ".zip" && IsHash(Path.GetFileNameWithoutExtension(f.Name))))
        {
            ct.ThrowIfCancellationRequested();
            var novelId = Guid.Parse(file.Directory!.Parent!.Name);
            var key = Path.GetFileNameWithoutExtension(file.Name);
            if (!keepArchives.Contains((novelId, key)) && file.LastWriteTimeUtc < cutoff)
            {
                Delete(file);
                continue;
            }
            // Retained ZIPs also pin chapter assemblies, so old downloads can be rebuilt cheaply.
            try
            {
                await using var zip = await ZipFile.OpenReadAsync(file.FullName, ct);
                if (zip.GetEntry("manifest.json") is not { } entry) continue;
                await using var input = await entry.OpenAsync(ct);
                var manifest = await JsonSerializer.DeserializeAsync<AudiobookArchiveManifest>(input,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web), ct);
                if (manifest?.Chapters is not null)
                    foreach (var chapter in manifest.Chapters.Where(chapter => chapter is not null && IsHash(chapter.ArtifactKey)))
                        required.Add(chapter.ArtifactKey);
            }
            catch (InvalidDataException) { /* Keep the ZIP; status/download already handles damaged archives. */ }
            catch (JsonException) { /* Existing job section references remain protected. */ }
        }
        var manifests = new List<(FileInfo File, AudioSourceManifest Value, bool Assembly)>();
        foreach (var file in cacheFiles.Where(f => f.Name.EndsWith(".source.json", StringComparison.Ordinal) && IsHash(f.Name[..^12])))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var value = JsonSerializer.Deserialize<AudioSourceManifest>(await File.ReadAllTextAsync(file.FullName, ct));
                if (value is null || value.Version != 1 || value.SourceKey != file.Name[..^12] ||
                    value.ArtifactKeys is not { Length: > 0 } || value.ArtifactKeys.Any(k => !IsHash(k))) throw new JsonException();
                manifests.Add((file, value, File.Exists(Path.Combine(cache, value.SourceKey + ".wav"))));
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Keeping shared audio because some dependency metadata could not be read.");
                return;
            }
        }
        foreach (var file in cacheFiles.Where(f => f.LastWriteTimeUtc >= cutoff && IsHash(Path.GetFileNameWithoutExtension(f.Name))))
            required.Add(Path.GetFileNameWithoutExtension(file.Name));
        var sectionKeys = required.ToHashSet();
        var pauses = saved.Select(j => j.Settings.EnableImmersive == true ? j.Settings.ImmersivePauseMs ?? 0 : 0).Append(0).Distinct().ToArray();
        var kept = new HashSet<string>();
        bool changed;
        do
        {
            changed = false;
            foreach (var (file, value, assembly) in manifests)
            {
                ct.ThrowIfCancellationRequested();
                if (kept.Contains(value.SourceKey)) continue;
                // The Section lookup also protects dependency maps created before assembly recipes existed.
                if (file.LastWriteTimeUtc >= cutoff || required.Contains(value.SourceKey) || (!assembly && value.ArtifactKeys.All(required.Contains)) ||
                    (!assembly && pauses.Any(p => sectionKeys.Contains(AudioCacheKey.Section(value.ArtifactKeys, p)))))
                {
                    kept.Add(value.SourceKey);
                    required.Add(value.SourceKey);
                    foreach (var key in value.ArtifactKeys) required.Add(key);
                    changed = true;
                }
            }
        } while (changed);
        foreach (var file in cacheFiles)
        {
            ct.ThrowIfCancellationRequested();
            if (file.LastWriteTimeUtc >= cutoff) continue;
            if (file.Name.EndsWith(".source.json", StringComparison.Ordinal))
            {
                var key = file.Name[..^12];
                if (IsHash(key) && !kept.Contains(key)) Delete(file);
            }
            else if (file.Extension is ".wav" or ".json")
            {
                var key = Path.GetFileNameWithoutExtension(file.Name);
                if (IsHash(key) && !required.Contains(key)) Delete(file);
            }
        }
    }

    /// <summary>Records successful reuse at most once per day using file timestamps; failures never interrupt playback.</summary>
    internal static void RecordAccess(string path)
    {
        try
        {
            var file = new FileInfo(path);
            if (file.Exists && (file.Attributes & FileAttributes.ReparsePoint) == 0 && file.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-1))
                file.LastWriteTimeUtc = DateTime.UtcNow;
        }
        catch (IOException) { /* Access timestamps are best effort and must not interrupt playback. */ }
        catch (UnauthorizedAccessException) { /* Read-only audio remains playable without refreshing its timestamp. */ }
    }

    private static bool IsHash(string? key) => key is { Length: 64 } && key.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static bool IsDirectory(string path) => Directory.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0;
    private static FileInfo[] Files(string directory) => IsDirectory(directory)
        ? new DirectoryInfo(directory).GetFiles().Where(f => (f.Attributes & FileAttributes.ReparsePoint) == 0).ToArray() : [];
    private static bool IsTemporary(string name)
    {
        if (!name.EndsWith(".tmp", StringComparison.Ordinal)) return false;
        var parts = name[..^4].Split('.');
        if (parts.Length == 1) return Guid.TryParseExact(parts[0], "N", out _);
        if (!Guid.TryParseExact(parts[^1], "N", out _)) return false;
        return (parts.Length == 3 && ((IsHash(parts[0]) && parts[1] is "wav" or "json" or "zip") ||
                (Guid.TryParse(parts[0], out _) && parts[1] == "json"))) ||
               (parts.Length == 4 && IsHash(parts[0]) && parts[1] == "source" && parts[2] == "json");
    }
    private void Delete(FileInfo file)
    {
        try { file.Delete(); }
        catch (IOException ex) { logger.LogWarning(ex, "A generated audio file could not be removed during cleanup."); }
        catch (UnauthorizedAccessException ex) { logger.LogWarning(ex, "A generated audio file is not writable during cleanup."); }
    }
}
