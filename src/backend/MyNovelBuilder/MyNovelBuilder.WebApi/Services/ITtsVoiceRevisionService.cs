using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Models.Tts;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>Captures and fingerprints the local recording consumed by a TTS provider.</summary>
public interface ITtsVoiceRevisionService
{
    /// <summary>Gets the current revision, or null for a voice without a local recording.</summary>
    Task<string?> GetRevisionAsync(TtsProvider provider, string voiceId, CancellationToken cancellationToken = default);

    /// <summary>Returns the exact bytes and metadata used to calculate the revision.</summary>
    Task<RecordedTtsVoice?> CaptureAsync(TtsProvider provider, string voiceId, CancellationToken cancellationToken = default);
}
