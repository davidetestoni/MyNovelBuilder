using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Models.Tts;

/// <summary>
/// Validated inputs for one synthesis call, including deferred streaming work.
/// Execution-only: never serialize endpoint URLs or reference recordings into snapshots.
/// </summary>
public sealed class TtsExecutionInputs(Uri? baseUri, RecordedTtsVoice? voice)
{
    /// <summary>The validated local provider endpoint.</summary>
    public Uri? BaseUri { get; } = baseUri;
    /// <summary>The validated recording, if the selected voice uses one.</summary>
    public RecordedTtsVoice? Voice { get; } = voice;
}

/// <summary>The same reference bytes and metadata that were checked against the snapshot revision.</summary>
public sealed class RecordedTtsVoice(byte[] audio, WritingLanguage language, string? transcript, string revision)
{
    private readonly byte[] _audio = (byte[])audio.Clone();
    /// <summary>The recorded voice language.</summary>
    public WritingLanguage Language { get; } = language;
    /// <summary>The reference transcript used by Audio8.</summary>
    public string? Transcript { get; } = transcript;
    /// <summary>The fingerprint of this recording and its consumed metadata.</summary>
    public string Revision { get; } = revision;
    /// <summary>Opens an independent read-only view of the captured bytes.</summary>
    public Stream OpenRead() => new MemoryStream(_audio, writable: false);
}
