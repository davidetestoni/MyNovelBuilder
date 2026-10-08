namespace MyNovelBuilder.WebApi.Services;

/// <summary>Hash-only mapping from frozen source/preprocessing inputs to ordered completed audio.</summary>
/// <param name="Version">Manifest schema version.</param>
/// <param name="SourceKey">Canonical source fingerprint.</param>
/// <param name="PreprocessingKey">Optional preprocessing fingerprint; never a text-bearing plan.</param>
/// <param name="ArtifactKeys">Ordered completed audio references.</param>
public sealed record AudioSourceManifest(int Version, string SourceKey, string? PreprocessingKey, string[] ArtifactKeys);

/// <summary>Versioned cache, separate from untrusted legacy playback entries.</summary>
public interface IAudioArtifactRepository
{
    /// <summary>Hold through lookup, generation and publication. Acquire source before synthesis keys.</summary>
    Task<IDisposable> AcquireAsync(string key, CancellationToken cancellationToken = default);
    /// <summary>Opens a validated seekable file stream, or returns null for missing/corrupt artifacts.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default);
    /// <summary>Reads validated audio bytes, or returns null on a cache miss.</summary>
    Task<byte[]?> ReadBytesAsync(string key, CancellationToken cancellationToken = default);
    /// <summary>Publishes complete WAV content and integrity metadata atomically per file.</summary>
    Task SaveAsync(string key, Stream wav, CancellationToken cancellationToken = default);
    /// <summary>Reads a hash-only mapping only when every referenced artifact is intact.</summary>
    Task<AudioSourceManifest?> ReadManifestAsync(string sourceKey, CancellationToken cancellationToken = default);
    /// <summary>Publishes a hash-only mapping after validating every referenced artifact.</summary>
    Task SaveManifestAsync(AudioSourceManifest manifest, CancellationToken cancellationToken = default);
}
