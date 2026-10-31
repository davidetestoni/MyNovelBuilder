using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MyNovelBuilder.WebApi.Models.AudioGeneration;
using MyNovelBuilder.WebApi.Options;
using MyNovelBuilder.WebApi.Services;
using NAudio.Wave;

namespace MyNovelBuilder.WebApi.Tests.Unit.Services;

public sealed class GeneratedAudioCleanupTests
{
    [Fact]
    public async Task RemovesOnlyOldOwnedTemporaryFiles()
    {
        using var f = new Fixture();
        var oldCache = f.Write($"audio/v1/{Key("temp")}.wav.{Guid.NewGuid():N}.tmp");
        var oldWork = f.Write($"audio/v1/work/{Guid.NewGuid():N}.tmp");
        var oldJob = f.Write($"audiobooks/{f.NovelId}/jobs/{Guid.NewGuid()}.json.{Guid.NewGuid():N}.tmp");
        var oldZip = f.Write($"audiobooks/{f.NovelId}/archives/{Key("zip")}.zip.{Guid.NewGuid():N}.tmp");
        var recent = f.Write($"audio/v1/work/{Guid.NewGuid():N}.tmp", days: 0);
        var unrelated = f.Write("audio/v1/notes.tmp");
        var legacy = f.Write($"audio/{Key("legacy")}.wav");
        var prose = f.Write($"static/{Guid.NewGuid():N}.tmp");
        await f.Cleanup.RunAsync();
        Assert.All(new[] { oldCache, oldWork, oldJob, oldZip }, path => Assert.False(File.Exists(path)));
        Assert.All(new[] { recent, unrelated, legacy, prose }, path => Assert.True(File.Exists(path)));
    }

    [Fact]
    public async Task RemovesExpiredArtifactsButKeepsJobSpeechAndItsDependencyChain()
    {
        using var f = new Fixture();
        var chunk = Key("chunk");
        var unit = Key("unit");
        var section = AudioCacheKey.Section([unit], 150);
        var chunkFile = f.Wave(chunk);
        var unitFile = f.Wave(unit);
        var sectionFile = f.Wave(section);
        var unitRecipe = f.Manifest(unit, chunk);
        var oldSource = f.Manifest(Key("source"), unit); // Existing source maps, before assembly recipes were added.
        await f.Job("failed", section, pause: 150);
        var expired = Key("unused");
        var unused = f.Wave(expired);
        var unusedMetadata = f.Write($"audio/v1/{expired}.json");
        var unusedSource = f.Manifest(Key("unused-source"), expired);
        var recent = f.Wave(Key("recent"), days: 0);
        await f.Cleanup.RunAsync();
        Assert.All(new[] { unused, unusedMetadata, unusedSource }, p => Assert.False(File.Exists(p)));
        Assert.All(new[] { chunkFile, unitFile, sectionFile, unitRecipe, oldSource, recent }, p => Assert.True(File.Exists(p)));
        Assert.Single(await f.Jobs.ListAsync());
    }

    [Fact]
    public async Task ASharedChunkDoesNotKeepEveryExpiredAssemblyVariant()
    {
        using var f = new Fixture();
        var shared = Key("shared");
        var unused = Key("unused-chunk");
        var variant = Key("old-variant");
        var sharedFile = f.Wave(shared, days: 0);
        var unusedFile = f.Wave(unused);
        var variantFile = f.Wave(variant);
        var recipe = f.Manifest(variant, shared, unused);
        await f.Cleanup.RunAsync();
        Assert.True(File.Exists(sharedFile));
        Assert.False(File.Exists(unusedFile));
        Assert.False(File.Exists(variantFile));
        Assert.False(File.Exists(recipe));
    }

    [Fact]
    public async Task ExpiresOldZipButKeepsLatestSuccessRecentDownloadsAndHistory()
    {
        using var f = new Fixture();
        var old = await f.Job("completed", archive: Key("old"), finishedDays: 200);
        var latest = await f.Job("completed", archive: Key("latest"), finishedDays: 100);
        await f.Job("failed", finishedDays: 1); // A newer failure must not displace the last successful download.
        var oldZip = f.Zip(old.Archive!.Key);
        var latestZip = f.Zip(latest.Archive!.Key);
        var recentZip = f.Zip(Key("recent-zip"), days: 0);
        var orphanZip = f.Zip(Key("orphan-zip"));
        await f.Cleanup.RunAsync();
        Assert.False(File.Exists(oldZip));
        Assert.False(File.Exists(orphanZip));
        Assert.True(File.Exists(latestZip));
        Assert.True(File.Exists(recentZip));
        Assert.Equal(3, (await f.Jobs.ListAsync()).Count);
    }

    [Fact]
    public async Task MissingNewestArchiveDoesNotExpirePreviousSuccessfulDownload()
    {
        using var f = new Fixture();
        await f.Job("completed", archive: Key("older"), finishedDays: 200);
        await f.Job("completed", archive: Key("missing"), finishedDays: 100);
        var previous = f.Zip(Key("older"));
        await f.Cleanup.RunAsync();
        Assert.True(File.Exists(previous));
    }

    [Fact]
    public async Task RetainedArchiveProtectsChapterAndUnderlyingAudio()
    {
        using var f = new Fixture();
        var chapter = Key("chapter");
        var audio = Key("section");
        var archive = Key("archive");
        await f.Job("completed", archive: archive);
        var chapterFile = f.Wave(chapter);
        var sectionFile = f.Wave(audio);
        var recipe = f.Manifest(chapter, audio);
        f.Zip(archive, chapter: chapter);
        await f.Cleanup.RunAsync();
        Assert.True(File.Exists(chapterFile));
        Assert.True(File.Exists(sectionFile));
        Assert.True(File.Exists(recipe));
    }

    [Fact]
    public async Task DamagedJobMetadataPreventsPermanentDeletion()
    {
        using var f = new Fixture();
        f.Write($"audiobooks/{f.NovelId}/jobs/{Guid.NewGuid()}.json", "{broken");
        var audio = f.Wave(Key("audio"));
        var zip = f.Zip(Key("archive"));
        await f.Cleanup.RunAsync();
        Assert.True(File.Exists(audio));
        Assert.True(File.Exists(zip));
    }

    [Fact]
    public async Task DamagedDependencyMetadataPreventsSharedAudioDeletion()
    {
        using var f = new Fixture();
        f.Write($"audio/v1/{Key("broken")}.source.json", "{broken");
        var audio = f.Wave(Key("audio"));
        await f.Cleanup.RunAsync();
        Assert.True(File.Exists(audio));
    }

    [Fact]
    public async Task DisabledExpiryStillRemovesAbandonedTemporaryFiles()
    {
        using var f = new Fixture(retentionDays: 0);
        var audio = f.Wave(Key("audio"));
        var zip = f.Zip(Key("archive"));
        var temporary = f.Write($"audio/v1/work/{Guid.NewGuid():N}.tmp");
        await f.Cleanup.RunAsync();
        Assert.True(File.Exists(audio));
        Assert.True(File.Exists(zip));
        Assert.False(File.Exists(temporary));
    }

    [Fact]
    public async Task SuccessfulCacheReuseRefreshesAgeAndKeepsAudio()
    {
        using var f = new Fixture();
        var repository = new FileSystemAudioArtifactRepository(f.Options);
        var key = Key("valid-audio");
        using var bytes = new MemoryStream();
        using (var wav = new WaveFileWriter(bytes, new WaveFormat(24000, 16, 1))) await wav.WriteAsync(new byte[480]);
        using var input = new MemoryStream(bytes.ToArray());
        await repository.SaveAsync(key, input);
        var source = Key("source");
        await repository.SaveManifestAsync(new(1, source, null, [key]));
        var paths = new[] { $"audio/v1/{key}.wav", $"audio/v1/{key}.json", $"audio/v1/{source}.source.json" }
            .Select(p => Path.Combine(f.Folder, p)).ToArray();
        foreach (var path in paths) File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-200));
        Assert.NotNull(await repository.ReadManifestAsync(source));
        await f.Cleanup.RunAsync();
        Assert.All(paths, path => Assert.True(File.GetLastWriteTimeUtc(path) > DateTime.UtcNow.AddDays(-1)));
        await using var reused = await repository.OpenReadAsync(key);
        Assert.NotNull(reused);
    }

    [Fact]
    public async Task SuccessfulDownloadRefreshesArchiveAge()
    {
        using var f = new Fixture();
        var key = Key("archive");
        var path = f.Zip(key);
        await using var zip = File.OpenRead(path);
        var hash = Convert.ToHexStringLower(await System.Security.Cryptography.SHA256.HashDataAsync(zip));
        await zip.DisposeAsync();
        var repository = new FileSystemAudioArtifactRepository(f.Options);
        var service = new AudiobookArchiveService(repository, new(repository, f.Options), f.Options);
        await using (var stream = await service.OpenAsync(f.NovelId, new(key, hash, new FileInfo(path).Length, "audiobook.zip"), default))
            Assert.NotNull(stream);
        await f.Cleanup.RunAsync();
        Assert.True(File.Exists(path));
        Assert.True(File.GetLastWriteTimeUtc(path) > DateTime.UtcNow.AddDays(-1));
    }

    [Fact]
    public async Task CompleteAssemblyRemainsReusableWhenAnInputIsMissing()
    {
        using var f = new Fixture();
        var repository = new FileSystemAudioArtifactRepository(f.Options);
        var inputKey = Key("input");
        using var bytes = new MemoryStream();
        using (var wav = new WaveFileWriter(bytes, new WaveFormat(24000, 16, 1))) await wav.WriteAsync(new byte[480]);
        using var input = new MemoryStream(bytes.ToArray());
        await repository.SaveAsync(inputKey, input);
        var assembler = new AudioArtifactAssembler(repository, f.Options);
        var complete = await assembler.AssembleAsync([inputKey], 0);
        File.Delete(Path.Combine(f.Folder, "audio", "v1", inputKey + ".wav"));
        Assert.Equal(complete, await assembler.AssembleAsync([inputKey], 0));
    }

    [Fact]
    public async Task DoesNotFollowCacheDirectorySymlinks()
    {
        if (OperatingSystem.IsWindows()) return; // Creating symlinks requires privileges on some Windows installations.
        using var f = new Fixture();
        var external = Path.Combine(Path.GetTempPath(), "mnb-external-audio-" + Guid.NewGuid());
        Directory.CreateDirectory(external);
        try
        {
            var file = Path.Combine(external, Key("outside") + ".wav");
            await File.WriteAllTextAsync(file, "unrelated");
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-200));
            Directory.CreateDirectory(Path.Combine(f.Folder, "audio"));
            Directory.CreateSymbolicLink(Path.Combine(f.Folder, "audio", "v1"), external);
            await f.Cleanup.RunAsync();
            Assert.True(File.Exists(file));
        }
        finally { Directory.Delete(external, true); }
    }

    [Fact]
    public async Task CancellationDoesNotDeleteArtifacts()
    {
        using var f = new Fixture();
        var audio = f.Wave(Key("audio"));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Cleanup.RunAsync(cancellation.Token));
        Assert.True(File.Exists(audio));
    }

    private static string Key(string value) => AudioCacheKey.Hash(value);
    private sealed class Fixture : IDisposable
    {
        public string Folder { get; } = Path.Combine(Path.GetTempPath(), "mnb-audio-cleanup-" + Guid.NewGuid());
        public Guid NovelId { get; } = Guid.NewGuid();
        public IOptions<AppStorageOptions> Options { get; }
        public FileSystemAudiobookJobRepository Jobs { get; }
        public GeneratedAudioCleanup Cleanup { get; }
        public Fixture(int retentionDays = 90)
        {
            Directory.CreateDirectory(Folder);
            Options = Microsoft.Extensions.Options.Options.Create(new AppStorageOptions { DataFolder = Folder, GeneratedAudioRetentionDays = retentionDays });
            Jobs = new(Options, NullLogger<FileSystemAudiobookJobRepository>.Instance);
            Cleanup = new(Options, Jobs, TimeProvider.System, NullLogger<GeneratedAudioCleanup>.Instance);
        }
        public string Write(string relative, string content = "data", int days = 200)
        {
            var path = Path.Combine(Folder, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-days));
            return path;
        }
        public string Wave(string key, int days = 200) => Write($"audio/v1/{key}.wav", days: days);
        public string Manifest(string key, params string[] dependencies) => Write($"audio/v1/{key}.source.json",
            JsonSerializer.Serialize(new AudioSourceManifest(1, key, null, dependencies)));
        public string Zip(string key, int days = 200, string? chapter = null)
        {
            var path = Path.Combine(Folder, "audiobooks", NovelId.ToString(), "archives", key + ".zip");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                using var writer = new StreamWriter(zip.CreateEntry("manifest.json").Open());
                var manifest = new AudiobookArchiveManifest(1, "Title", "Author", DateTimeOffset.UtcNow, new(), "pcm-24000-mono-16", 500,
                    chapter is null ? [] : [new(0, "Chapter", "001-chapter.wav", chapter, Key("checksum"), 1, [])], [], [], []);
                writer.Write(JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            }
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-days));
            return path;
        }
        public async Task<AudiobookJob> Job(string state, string? section = null, string? archive = null, int pause = 0, int finishedDays = 100)
        {
            var job = new AudiobookJob { Id = Guid.NewGuid(), NovelId = NovelId, InputChecksum = Key("input"), State = state,
                CreatedAt = DateTime.UtcNow.AddDays(-finishedDays), FinishedAt = DateTime.UtcNow.AddDays(-finishedDays),
                Settings = new(EnableImmersive: pause > 0, ImmersivePauseMs: pause),
                Sections = section is null ? [] : [new() { ArtifactKey = section, State = "generated" }],
                Archive = archive is null ? null : new(archive, Key("checksum"), 0, "audiobook.zip") };
            await Jobs.SaveAsync(job);
            return job;
        }
        public void Dispose() => Directory.Delete(Folder, true);
    }
}
