using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MyNovelBuilder.WebApi.Options;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Atomically published WAVs with integrity sidecars. A missing/invalid sidecar is a cache miss.
/// Coordination spans repository instances in this process; job execution leases belong to the worker.
/// </summary>
public sealed class FileSystemAudioArtifactRepository(IOptions<AppStorageOptions> options) : IAudioArtifactRepository
{
    private readonly string _folder = Path.Combine(options.Value.DataFolder, "audio", "v1");
    private static readonly object Gate = new();
    private static readonly Dictionary<string, Entry> Locks = new(StringComparer.Ordinal);
    private sealed class Entry { public readonly SemaphoreSlim Semaphore = new(1); public int Users; }
    private sealed class Lease(Action release) : IDisposable
    {
        private Action? _release = release;
        public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
    }
    private sealed record Integrity(int Version, string Key, long Length, string Sha256);

    private static bool IsKey(string? key) => key is { Length: 64 } && key.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private string PathFor(string key, string extension)
    {
        if (!IsKey(key)) throw new ArgumentException("Expected a lowercase SHA-256 cache key.", nameof(key));
        return Path.Combine(_folder, key + extension);
    }

    /// <inheritdoc />
    public async Task<IDisposable> AcquireAsync(string key, CancellationToken cancellationToken = default)
    {
        var lockKey = Path.GetFullPath(PathFor(key, ".lock"));
        Entry entry;
        lock (Gate)
        {
            if (!Locks.TryGetValue(lockKey, out entry!)) Locks[lockKey] = entry = new Entry();
            entry.Users++;
        }
        void ReleaseReference()
        {
            lock (Gate)
            {
                if (--entry.Users == 0) { Locks.Remove(lockKey); entry.Semaphore.Dispose(); }
            }
        }
        try { await entry.Semaphore.WaitAsync(cancellationToken); }
        catch { ReleaseReference(); throw; }
        return new Lease(() => { entry.Semaphore.Release(); ReleaseReference(); });
    }

    /// <inheritdoc />
    public async Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = PathFor(key, ".wav");
        FileStream? stream = null;
        try
        {
            var metadata = JsonSerializer.Deserialize<Integrity>(await File.ReadAllTextAsync(PathFor(key, ".json"), cancellationToken));
            if (metadata is null || metadata.Version != 1 || metadata.Key != key || !IsKey(metadata.Sha256)) return null;
            stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 65536, true);
            if (stream.Length != metadata.Length || !ValidateWav(stream)) return null;
            stream.Position = 0;
            var checksum = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
            if (checksum != metadata.Sha256) return null;
            stream.Position = 0;
            var result = stream;
            stream = null;
            return result;
        }
        catch (IOException) { return null; }
        catch (JsonException) { return null; }
        finally { if (stream is not null) await stream.DisposeAsync(); }
    }

    /// <inheritdoc />
    public async Task<byte[]?> ReadBytesAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var stream = await OpenReadAsync(key, cancellationToken);
        if (stream is null) return null;
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    /// <inheritdoc />
    public async Task SaveAsync(string key, Stream wav, CancellationToken cancellationToken = default)
    {
        var path = PathFor(key, ".wav");
        Directory.CreateDirectory(_folder);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Integrity metadata;
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536, true))
            {
                await wav.CopyToAsync(file, cancellationToken);
                if (!ValidateWav(file)) throw new InvalidDataException("Cannot cache an incomplete or invalid WAV file.");
                file.Position = 0;
                metadata = new Integrity(1, key, file.Length, Convert.ToHexStringLower(await SHA256.HashDataAsync(file, cancellationToken)));
                file.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
            await PublishJsonAsync(PathFor(key, ".json"), metadata, cancellationToken);
        }
        finally { File.Delete(temporary); }
    }

    /// <inheritdoc />
    public async Task<AudioSourceManifest?> ReadManifestAsync(string sourceKey, CancellationToken cancellationToken = default)
    {
        var path = PathFor(sourceKey, ".source.json");
        try
        {
            var manifest = JsonSerializer.Deserialize<AudioSourceManifest>(await File.ReadAllTextAsync(path, cancellationToken));
            if (manifest is null || manifest.Version != 1 || manifest.SourceKey != sourceKey ||
                (manifest.PreprocessingKey is not null && !IsKey(manifest.PreprocessingKey)) ||
                manifest.ArtifactKeys is not { Length: > 0 } || manifest.ArtifactKeys.Any(k => !IsKey(k))) return null;
            foreach (var key in manifest.ArtifactKeys)
            {
                await using var audio = await OpenReadAsync(key, cancellationToken);
                if (audio is null) return null;
            }
            return manifest;
        }
        catch (IOException) { return null; }
        catch (JsonException) { return null; }
    }

    /// <inheritdoc />
    public async Task SaveManifestAsync(AudioSourceManifest manifest, CancellationToken cancellationToken = default)
    {
        var path = PathFor(manifest.SourceKey, ".source.json");
        if (manifest.Version != 1 || manifest.ArtifactKeys is not { Length: > 0 } ||
            manifest.ArtifactKeys.Any(k => !IsKey(k)) ||
            (manifest.PreprocessingKey is not null && !IsKey(manifest.PreprocessingKey)))
            throw new ArgumentException("Manifest must contain only versioned hashes and artifact references.", nameof(manifest));
        foreach (var key in manifest.ArtifactKeys)
        {
            await using var audio = await OpenReadAsync(key, cancellationToken);
            if (audio is null) throw new InvalidDataException("Cannot publish a manifest with missing or corrupt audio.");
        }
        await PublishJsonAsync(path, manifest, cancellationToken);
    }

    private async Task PublishJsonAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_folder);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(file, value, cancellationToken: cancellationToken);
                file.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
        }
        finally { File.Delete(temporary); }
    }

    // Parse chunk bounds as well as the format: tolerant WAV readers can accept truncated data.
    private static bool ValidateWav(Stream stream)
    {
        if (stream.Length < 44) return false;
        stream.Position = 0;
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
        if (reader.ReadUInt32() != 0x46464952 || reader.ReadUInt32() != stream.Length - 8 || reader.ReadUInt32() != 0x45564157) return false;
        var format = false;
        var data = false;
        ushort blockAlign = 0;
        while (stream.Position + 8 <= stream.Length)
        {
            var id = reader.ReadUInt32();
            var length = reader.ReadUInt32();
            var end = stream.Position + length;
            if (end > stream.Length) return false;
            if (id == 0x20746d66)
            {
                if (length < 16) return false;
                var encoding = reader.ReadUInt16();
                var channels = reader.ReadUInt16();
                var rate = reader.ReadUInt32();
                var bytesPerSecond = reader.ReadUInt32();
                blockAlign = reader.ReadUInt16();
                var bits = reader.ReadUInt16();
                if (encoding is not (1 or 3) || channels == 0 || rate == 0 || bits == 0 || bits % 8 != 0 ||
                    blockAlign != channels * (bits / 8) || bytesPerSecond != (long)rate * blockAlign) return false;
                format = true;
            }
            if (id == 0x61746164)
            {
                if (!format || length == 0 || length % blockAlign != 0) return false;
                data = true;
            }
            stream.Position = end + (length & 1);
        }
        return format && data && stream.Position == stream.Length;
    }
}
