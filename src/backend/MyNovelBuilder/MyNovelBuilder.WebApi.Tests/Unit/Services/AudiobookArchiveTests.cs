using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Models.AudioGeneration;
using MyNovelBuilder.WebApi.Options;
using MyNovelBuilder.WebApi.Services;
using NAudio.Wave;

namespace MyNovelBuilder.WebApi.Tests.Unit.Services;

public sealed class AudiobookArchiveTests
{
    [Fact]
    public async Task NativeAndMixedChapterFormatsHaveActualFrameBasedOffsetsAndProfiles()
    {
        using var f = new Fixture();
        var a = await f.Save(1, 8820, 44100, 1); // 100 ms, mono.
        var b = await f.Save(2, 38400, 48000, 2); // 200 ms, stereo.
        var progress = new List<AudiobookSectionProgress>
        {
            new() { ChapterIndex = 0, SectionIndex = 0, State = "generated", ArtifactKey = a },
            new() { ChapterIndex = 0, SectionIndex = 2, State = "generated", ArtifactKey = b },
            new() { ChapterIndex = 2, SectionIndex = 0, State = "generated", ArtifactKey = a }
        };
        var reference = await f.Build(progress);
        var manifest = await f.Manifest(reference);
        Assert.Equal("per-chapter", manifest.OutputProfile);
        Assert.Equal("pcm-48000-2ch-16", manifest.Chapters[0].OutputProfile);
        Assert.Equal("pcm-44100-1ch-16", manifest.Chapters[1].OutputProfile);
        Assert.Equal(0.8, manifest.Chapters[0].DurationSeconds, 4);
        Assert.Equal(0.6, manifest.Chapters[0].Sections[1].OffsetSeconds, 4);
        Assert.Equal(0.2, manifest.Chapters[0].Sections[1].DurationSeconds, 6);
        Assert.Equal(0.1, manifest.Chapters[1].DurationSeconds, 6);
    }

    [Fact]
    public async Task ChaptersPreservePcmOrderGapsOffsetsAndOriginalNumbering_ManifestExcludesPrivateInputs()
    {
        using var f = new Fixture();
        var progress = await f.Progress();
        var stages = new List<(string, int)>();
        var reference = await f.Service.BuildAsync(f.Snapshot, progress, (s, n) => { stages.Add((s, n)); return Task.CompletedTask; }, default);
        await using var file = await f.Service.OpenAsync(f.Snapshot.NovelId, reference, default);
        Assert.NotNull(file);
        using var zip = new ZipArchive(file);
        Assert.Equal(new[] { "001-evil_title.wav", "003-evil_title.wav", "manifest.json" }, zip.Entries.Select(e => e.FullName));
        var manifest = await ReadManifest(zip);
        Assert.Equal(new[] { 1 }, manifest.SkippedChapters);
        Assert.Contains(manifest.SkippedSections, s => s.ChapterIndex == 0 && s.SectionIndex == 1);
        var chapter = manifest.Chapters[0];
        Assert.Equal(0.53, chapter.DurationSeconds, 6);
        Assert.Equal(0.51, chapter.Sections[1].OffsetSeconds, 6);
        Assert.Equal(0.02, chapter.Sections[1].DurationSeconds, 6);
        await using var entry = await zip.Entries[0].OpenAsync();
        using var copy = new MemoryStream();
        await entry.CopyToAsync(copy);
        copy.Position = 0;
        using var wav = new WaveFileReader(copy);
        Assert.Equal(24000, wav.WaveFormat.SampleRate);
        Assert.Equal(1, wav.WaveFormat.Channels);
        Assert.Equal(16, wav.WaveFormat.BitsPerSample);
        var pcm = new byte[wav.Length];
        Assert.Equal(pcm.Length, await wav.ReadAsync(pcm.AsMemory()));
        Assert.Equal(25440, pcm.Length); // 480 + 24000 + 960, with no final gap.
        Assert.All(pcm.Take(480), b => Assert.Equal((byte)1, b));
        Assert.All(pcm.Skip(480).Take(24000), b => Assert.Equal((byte)0, b));
        Assert.All(pcm.Skip(24480), b => Assert.Equal((byte)2, b));
        var json = JsonSerializer.Serialize(manifest);
        Assert.DoesNotContain("PRIVATE", json);
        Assert.DoesNotContain("https://", json);
        Assert.Equal(("packaging", 2), stages[^1]);
    }

    [Fact]
    public async Task MissingChaptersAndZipRebuildFromSections_AndRenameOnlyChangesArchive()
    {
        using var f = new Fixture();
        var progress = await f.Progress();
        var first = await f.Build(progress);
        var manifest = await f.Manifest(first);
        var zipPath = f.ZipPath(first);
        var time = File.GetLastWriteTimeUtc(zipPath);
        Assert.Equal(first, await f.Build(progress));
        Assert.Equal(time, File.GetLastWriteTimeUtc(zipPath));
        File.Delete(zipPath);
        foreach (var chapter in manifest.Chapters)
            File.Delete(Path.Combine(f.Folder, "audio", "v1", chapter.ArtifactKey + ".wav"));
        var rebuilt = await f.Build(progress);
        Assert.True(f.Service.IsAvailable(f.Snapshot.NovelId, rebuilt));
        var renamed = f.Snapshot with { Chapters = f.Snapshot.Chapters.SetItem(0, f.Snapshot.Chapters[0] with { Title = "Renamed" }) };
        var renamedReference = await f.Service.BuildAsync(renamed, progress, (_, _) => Task.CompletedTask, default);
        var renamedManifest = await f.Manifest(renamedReference);
        Assert.Equal(manifest.Chapters.Select(c => c.ArtifactKey), renamedManifest.Chapters.Select(c => c.ArtifactKey));
        Assert.NotEqual(first.Key, renamedReference.Key);
    }

    [Fact]
    public async Task CorruptArchiveIsNotServed_AndRebuildReplacesIt()
    {
        using var f = new Fixture();
        var progress = await f.Progress();
        var reference = await f.Build(progress);
        var bytes = await File.ReadAllBytesAsync(f.ZipPath(reference));
        bytes[0] ^= 255;
        await File.WriteAllBytesAsync(f.ZipPath(reference), bytes);
        Assert.Null(await f.Service.OpenAsync(f.Snapshot.NovelId, reference, default));
        var rebuilt = await f.Build(progress);
        await using var result = await f.Service.OpenAsync(f.Snapshot.NovelId, rebuilt, default);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task CancellationDuringPackagingPublishesNothing_AndKeepsReusableChapters()
    {
        using var f = new Fixture();
        var progress = await f.Progress();
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Service.BuildAsync(f.Snapshot, progress, (stage, _) =>
        {
            if (stage == "packaging") cancellation.Cancel();
            return Task.CompletedTask;
        }, cancellation.Token));
        Assert.Empty(Directory.GetFiles(f.Folder, "*.zip", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFiles(f.Folder, "*.tmp", SearchOption.AllDirectories));
        Assert.NotNull(await f.Build(progress));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailureOrCancellationAfterFirstZipEntryRemovesPartialArchive(bool cancel)
    {
        using var f = new Fixture();
        var progress = await f.Progress();
        using var cancellation = new CancellationTokenSource();
        var repository = new FailingReads(f.Artifacts);
        var options = Microsoft.Extensions.Options.Options.Create(new AppStorageOptions { DataFolder = f.Folder });
        var service = new AudiobookArchiveService(repository, new AudioArtifactAssembler(f.Artifacts, options), options);
        var run = service.BuildAsync(f.Snapshot, progress, (stage, _) =>
        {
            if (stage == "packaging")
            {
                repository.Remaining = 1;
                repository.Fail = () =>
                {
                    if (cancel) { cancellation.Cancel(); cancellation.Token.ThrowIfCancellationRequested(); }
                    throw new IOException("Simulated disk read failure after first chapter was copied.");
                };
            }
            return Task.CompletedTask;
        }, cancellation.Token);
        if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        else await Assert.ThrowsAsync<IOException>(() => run);
        Assert.Empty(Directory.GetFiles(f.Folder, "*.zip", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFiles(f.Folder, "*.tmp", SearchOption.AllDirectories));
        Assert.NotNull(await f.Build(progress));
    }

    [Fact]
    public async Task MissingSectionNeverPublishesPartialArchive_AndEmptyNovelIsRejected()
    {
        using var f = new Fixture();
        var progress = await f.Progress();
        File.Delete(Path.Combine(f.Folder, "audio", "v1", progress[1].ArtifactKey + ".wav"));
        await Assert.ThrowsAsync<InvalidDataException>(() => f.Build(progress));
        Assert.Empty(Directory.GetFiles(f.Folder, "*.zip", SearchOption.AllDirectories));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.BuildAsync(f.Snapshot with { Chapters = [] }, [], (_, _) => Task.CompletedTask, default));
    }

    [Fact]
    public void StorageLimitsHaveActionableCodesBeforeWriting()
    {
        using var f = new Fixture();
        var disk = Assert.Throws<AudiobookExportException>(() => AudiobookStorage.EnsureSpace(f.Folder, long.MaxValue));
        Assert.Equal("insufficient_disk_space", disk.Code);
        AudiobookStorage.EnsureWaveSize(0, uint.MaxValue - 44L);
        var size = Assert.Throws<AudiobookExportException>(() => AudiobookStorage.EnsureWaveSize(uint.MaxValue - 44L, 2));
        Assert.Equal("wav_size_limit_split_chapter", size.Code);
    }

    [Theory]
    [InlineData("../../CON\\evil\u202e?.", "CON_evil")]
    [InlineData("CON", "_CON")]
    [InlineData("...", "chapter")]
    [InlineData("Caffè 日本語", "Caffè 日本語")]
    public void FilenamesArePortable(string title, string expected)
        => Assert.Equal(expected, AudiobookArchiveService.SafeName(title, "chapter"));

    private static async Task<AudiobookArchiveManifest> ReadManifest(ZipArchive zip)
    {
        await using var entry = await zip.GetEntry("manifest.json")!.OpenAsync();
        return (await JsonSerializer.DeserializeAsync<AudiobookArchiveManifest>(entry, new JsonSerializerOptions(JsonSerializerDefaults.Web)))!;
    }

    private sealed class FailingReads(IAudioArtifactRepository inner) : IAudioArtifactRepository
    {
        public int Remaining = int.MaxValue;
        public Action? Fail;
        public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default)
        {
            if (Remaining-- == 0) Fail!();
            return inner.OpenReadAsync(key, cancellationToken);
        }
        public Task<IDisposable> AcquireAsync(string key, CancellationToken cancellationToken = default) => inner.AcquireAsync(key, cancellationToken);
        public Task<byte[]?> ReadBytesAsync(string key, CancellationToken cancellationToken = default) => inner.ReadBytesAsync(key, cancellationToken);
        public Task SaveAsync(string key, Stream wav, CancellationToken cancellationToken = default) => inner.SaveAsync(key, wav, cancellationToken);
        public Task<AudioSourceManifest?> ReadManifestAsync(string sourceKey, CancellationToken cancellationToken = default) => inner.ReadManifestAsync(sourceKey, cancellationToken);
        public Task SaveManifestAsync(AudioSourceManifest manifest, CancellationToken cancellationToken = default) => inner.SaveManifestAsync(manifest, cancellationToken);
    }

    private sealed class Fixture : IDisposable
    {
        public string Folder { get; } = Path.Combine(Path.GetTempPath(), "audiobook-archive-" + Guid.NewGuid());
        public FileSystemAudioArtifactRepository Artifacts { get; }
        public AudiobookArchiveService Service { get; }
        public AudiobookSnapshot Snapshot { get; } = new(Guid.NewGuid(), "../Novel", "Author", DateTimeOffset.UtcNow,
            new(TtsProvider.ElevenLabs, "model", "voice", false, false, 0, default, "text-model", null, "https://PRIVATE", "revision"),
            [new(0, "../evil/title", [new(0, "PRIVATE first", [], []), new(1, "", [], []), new(2, "PRIVATE second", [], [])]),
             new(1, "Empty", []), new(2, "../evil/title", [new(0, "PRIVATE first", [], [])])]);
        public Fixture()
        {
            Directory.CreateDirectory(Folder);
            var options = Microsoft.Extensions.Options.Options.Create(new AppStorageOptions { DataFolder = Folder });
            Artifacts = new(options);
            Service = new(Artifacts, new AudioArtifactAssembler(Artifacts, options), options);
        }
        public async Task<List<AudiobookSectionProgress>> Progress()
        {
            var a = await Save(1, 480);
            var b = await Save(2, 960);
            return [new() { ChapterIndex = 0, SectionIndex = 0, State = "generated", ArtifactKey = a },
                    new() { ChapterIndex = 0, SectionIndex = 2, State = "generated", ArtifactKey = b },
                    new() { ChapterIndex = 2, SectionIndex = 0, State = "reused", ArtifactKey = a }];
        }
        public async Task<string> Save(byte value, int count, int rate = 24000, int channels = 1)
        {
            var key = AudioCacheKey.Hash($"{value}-{rate}-{channels}");
            using var bytes = new MemoryStream();
            using (var writer = new WaveFileWriter(bytes, new WaveFormat(rate, 16, channels))) await writer.WriteAsync(Enumerable.Repeat(value, count).ToArray());
            using var input = new MemoryStream(bytes.ToArray());
            await Artifacts.SaveAsync(key, input);
            return key;
        }
        public Task<AudiobookArchiveReference> Build(List<AudiobookSectionProgress> progress) => Service.BuildAsync(Snapshot, progress, (_, _) => Task.CompletedTask, default);
        public string ZipPath(AudiobookArchiveReference reference) => Path.Combine(Folder, "audiobooks", Snapshot.NovelId.ToString(), "archives", reference.Key + ".zip");
        public async Task<AudiobookArchiveManifest> Manifest(AudiobookArchiveReference reference)
        {
            await using var file = await Service.OpenAsync(Snapshot.NovelId, reference, default);
            using var zip = new ZipArchive(file!);
            return await ReadManifest(zip);
        }
        public void Dispose() => Directory.Delete(Folder, true);
    }
}
