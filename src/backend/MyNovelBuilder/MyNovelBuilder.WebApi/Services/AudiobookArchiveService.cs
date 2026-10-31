using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MyNovelBuilder.WebApi.Models.AudioGeneration;
using MyNovelBuilder.WebApi.Options;
using NAudio.Wave;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>Assembles cached sections into chapters and atomically publishes a private, validated ZIP.</summary>
public sealed class AudiobookArchiveService(IAudioArtifactRepository artifacts, AudioArtifactAssembler assembler,
    IOptions<AppStorageOptions> storage)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>Builds chapter audio and the export manifest without any provider calls. Reports each completed chapter.</summary>
    public async Task<AudiobookArchiveReference> BuildAsync(AudiobookSnapshot snapshot, IReadOnlyList<AudiobookSectionProgress> progress,
        Func<string, int, Task> report, CancellationToken ct)
    {
        var chapters = new List<AudiobookArchiveChapter>();
        var skippedChapters = new List<int>();
        var skippedSections = new List<AudiobookSkippedSection>();
        var digits = Math.Max(3, snapshot.Chapters.Length.ToString(CultureInfo.InvariantCulture).Length);
        await report("assembly", 0);
        foreach (var chapter in snapshot.Chapters)
        {
            ct.ThrowIfCancellationRequested();
            var keys = new List<string>();
            var offsets = new List<AudiobookArchiveSection>();
            var sectionIndices = new List<int>();
            foreach (var section in chapter.Sections)
            {
                if (string.IsNullOrWhiteSpace(section.SpeechText))
                {
                    skippedSections.Add(new(chapter.ChapterIndex, section.SectionIndex));
                    continue;
                }
                var saved = progress.Single(s => s.ChapterIndex == chapter.ChapterIndex && s.SectionIndex == section.SectionIndex);
                if (saved.State is not ("generated" or "reused") || saved.ArtifactKey is null)
                    throw new InvalidDataException("A spoken section has not finished rendering.");
                keys.Add(saved.ArtifactKey);
                sectionIndices.Add(section.SectionIndex);
            }
            if (keys.Count == 0) { skippedChapters.Add(chapter.ChapterIndex); continue; }
            var key = await assembler.AssembleChapterAsync(keys, ct);
            var assembly = await artifacts.ReadManifestAsync(key, ct);
            if (assembly?.SegmentFrames is not { } segmentFrames || segmentFrames.Length != keys.Count)
                throw new InvalidDataException("Chapter segment timings are missing.");
            await using var audio = await artifacts.OpenReadAsync(key, ct) ?? throw new InvalidDataException("Chapter publication failed.");
            var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(audio, ct));
            audio.Position = 0;
            using var reader = new WaveFileReader(audio);
            var format = reader.WaveFormat;
            long frames = 0;
            for (var index = 0; index < segmentFrames.Length; index++)
            {
                if (index > 0) frames += (long)format.SampleRate * 500 / 1000;
                offsets.Add(new(sectionIndices[index], frames / (double)format.SampleRate, segmentFrames[index] / (double)format.SampleRate));
                frames += segmentFrames[index];
            }
            if (frames != reader.Length / format.BlockAlign)
                throw new InvalidDataException("Chapter segment timings do not match its audio.");
            var name = (chapter.ChapterIndex + 1).ToString("D" + digits, CultureInfo.InvariantCulture) + "-" + SafeName(chapter.Title, "chapter") + ".wav";
            var profile = $"{(format.Encoding == WaveFormatEncoding.Pcm ? "pcm" : "float")}-{format.SampleRate}-{format.Channels}ch-{format.BitsPerSample}";
            chapters.Add(new(chapter.ChapterIndex, chapter.Title, name, key, hash, frames / (double)format.SampleRate, offsets.ToArray(), profile));
            await report("assembly", chapters.Count);
        }
        if (chapters.Count == 0) throw new InvalidOperationException("The novel has no spoken prose.");
        var profiles = chapters.Select(c => c.OutputProfile).Distinct().ToArray();
        var manifest = new AudiobookArchiveManifest(1, snapshot.Title, snapshot.Author, snapshot.CapturedAt,
            AudiobookJobService.ToRequest(snapshot.Settings), profiles.Length == 1 ? profiles[0] : "per-chapter", 500, chapters.ToArray(),
            skippedChapters.ToArray(), skippedSections.ToArray(),
            skippedChapters.Count + skippedSections.Count > 0 ? ["Empty chapters and sections were omitted; original numbering is preserved."] : []);
        var archiveKey = AudioCacheKey.Create("archive-v1", manifest);
        // The snapshot date is exported metadata, so a new preview may need a new ZIP even when all audio is reused.
        var path = PathFor(snapshot.NovelId, archiveKey);
        using var lease = await artifacts.AcquireAsync(archiveKey, ct);
        await report("packaging", chapters.Count);
        if (File.Exists(path))
        {
            try
            {
                await using var existing = OpenFile(path);
                await ValidateAsync(existing, manifest, ct);
                existing.Position = 0;
                GeneratedAudioCleanup.RecordAccess(path);
                return new(archiveKey, Convert.ToHexStringLower(await SHA256.HashDataAsync(existing, ct)), existing.Length,
                    SafeName(snapshot.Title, "novel") + "-audiobook.zip");
            }
            catch (InvalidDataException) { /* Rebuild damaged archives from retained audio. */ }
            catch (JsonException) { /* A damaged manifest also requires rebuilding. */ }
        }
        var directory = Path.GetDirectoryName(path)!;
        ct.ThrowIfCancellationRequested();
        Directory.CreateDirectory(directory);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536, true))
            {
                using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
                {
                    foreach (var chapter in chapters)
                    {
                        ct.ThrowIfCancellationRequested();
                        await using var audio = await artifacts.OpenReadAsync(chapter.ArtifactKey, ct)
                            ?? throw new InvalidDataException("Chapter audio is missing or corrupt.");
                        AudiobookStorage.EnsureSpace(directory, audio.Length + 65536);
                        var entry = zip.CreateEntry(chapter.FileName, CompressionLevel.NoCompression);
                        await using var destination = await entry.OpenAsync(ct);
                        await audio.CopyToAsync(destination, ct);
                    }
                    await using var metadata = await zip.CreateEntry("manifest.json", CompressionLevel.Optimal).OpenAsync(ct);
                    await JsonSerializer.SerializeAsync(metadata, manifest, Json, ct);
                }
                output.Flush(flushToDisk: true);
                output.Position = 0;
                await ValidateAsync(output, manifest, ct);
                output.Position = 0;
                var reference = new AudiobookArchiveReference(archiveKey,
                    Convert.ToHexStringLower(await SHA256.HashDataAsync(output, ct)), output.Length,
                    SafeName(snapshot.Title, "novel") + "-audiobook.zip");
                // Close the file before renaming it, including on Windows.
                await output.DisposeAsync();
                ct.ThrowIfCancellationRequested();
                File.Move(temporary, path, overwrite: true);
                return reference;
            }
        }
        finally { File.Delete(temporary); }
    }

    /// <summary>Checks whether the saved ZIP still exists with the expected size. Download also verifies its checksum.</summary>
    public bool IsAvailable(Guid novelId, AudiobookArchiveReference? reference)
    {
        if (reference is null || !IsHash(reference.Key) || !IsHash(reference.Sha256)) return false;
        try { return new FileInfo(PathFor(novelId, reference.Key)) is { Exists: true } info && info.Length == reference.Length; }
        catch (IOException) { return false; }
    }

    /// <summary>Opens a seekable ZIP after verifying integrity, or returns null when it is missing or damaged.</summary>
    public async Task<Stream?> OpenAsync(Guid novelId, AudiobookArchiveReference? reference, CancellationToken ct)
    {
        if (!IsAvailable(novelId, reference)) return null;
        FileStream? stream = null;
        try
        {
            stream = OpenFile(PathFor(novelId, reference!.Key));
            if (Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct)) != reference.Sha256) return null;
            stream.Position = 0;
            GeneratedAudioCleanup.RecordAccess(PathFor(novelId, reference.Key));
            var result = stream;
            stream = null;
            return result;
        }
        catch (IOException) { return null; }
        finally { if (stream is not null) await stream.DisposeAsync(); }
    }

    /// <summary>Produces a bounded portable filename stem, removing separators, control characters and unsafe Unicode.</summary>
    public static string SafeName(string title, string fallback)
    {
        var result = new StringBuilder();
        foreach (var rune in title.EnumerateRunes())
        {
            if (result.Length + rune.Utf16SequenceLength > 80) break;
            var category = Rune.GetUnicodeCategory(rune);
            if (Rune.IsLetterOrDigit(rune) || category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark || rune.Value is ' ' or '-' or '_')
                result.Append(rune.ToString());
            else result.Append('_');
        }
        var name = result.ToString().Trim(' ', '_', '-');
        if (name.Length == 0) name = fallback;
        if (new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }.Contains(name, StringComparer.OrdinalIgnoreCase))
            name = "_" + name;
        return name;
    }

    private string PathFor(Guid novelId, string key) => Path.Combine(storage.Value.DataFolder, "audiobooks", novelId.ToString(), "archives", key + ".zip");
    private static FileStream OpenFile(string path) => new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536, true);
    private static bool IsHash(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static async Task ValidateAsync(Stream stream, AudiobookArchiveManifest manifest, CancellationToken ct)
    {
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        if (zip.Entries.Count != manifest.Chapters.Length + 1) throw new InvalidDataException("Incomplete audiobook ZIP.");
        foreach (var chapter in manifest.Chapters)
        {
            var entry = zip.GetEntry(chapter.FileName) ?? throw new InvalidDataException("Missing chapter in ZIP.");
            await using var audio = await entry.OpenAsync(ct);
            if (Convert.ToHexStringLower(await SHA256.HashDataAsync(audio, ct)) != chapter.Sha256)
                throw new InvalidDataException("Chapter checksum mismatch in ZIP.");
        }
        await using var metadata = await (zip.GetEntry("manifest.json") ?? throw new InvalidDataException("Missing export manifest.")).OpenAsync(ct);
        var saved = await JsonSerializer.DeserializeAsync<AudiobookArchiveManifest>(metadata, Json, ct);
        if (saved is null || AudioCacheKey.Create("archive-v1", saved) != AudioCacheKey.Create("archive-v1", manifest))
            throw new InvalidDataException("Export manifest mismatch.");
    }
}
