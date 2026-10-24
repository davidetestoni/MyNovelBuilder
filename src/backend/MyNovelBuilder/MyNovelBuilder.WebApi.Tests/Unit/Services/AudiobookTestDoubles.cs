using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Models.Tts;
using MyNovelBuilder.WebApi.Services;

namespace MyNovelBuilder.WebApi.Tests.Unit.Services;

internal sealed class NoRecordedVoices : ITtsVoiceRevisionService
{
    public Task<string?> GetRevisionAsync(TtsProvider provider, string voiceId, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);
    public Task<RecordedTtsVoice?> CaptureAsync(TtsProvider provider, string voiceId, CancellationToken cancellationToken = default)
        => Task.FromResult<RecordedTtsVoice?>(null);
}
