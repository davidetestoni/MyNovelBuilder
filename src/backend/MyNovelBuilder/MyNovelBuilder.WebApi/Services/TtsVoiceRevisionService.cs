using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Options;
using MyNovelBuilder.WebApi.Models.Tts;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>Fingerprints local reference recordings and their consumed voice metadata.</summary>
public sealed class TtsVoiceRevisionService(
    AppDbContext database,
    IOptions<AppStorageOptions> storage) : ITtsVoiceRevisionService
{
    /// <inheritdoc />
    public async Task<string?> GetRevisionAsync(
        TtsProvider provider, string voiceId, CancellationToken cancellationToken = default)
        => (await CaptureAsync(provider, voiceId, cancellationToken))?.Revision;

    /// <inheritdoc />
    public async Task<RecordedTtsVoice?> CaptureAsync(
        TtsProvider provider, string voiceId, CancellationToken cancellationToken = default)
    {
        if (provider is not (TtsProvider.Qwen3 or TtsProvider.OmniVoice
            or TtsProvider.Chatterbox or TtsProvider.Audio8 or TtsProvider.KittenTts)
            || provider is (TtsProvider.Audio8 or TtsProvider.Chatterbox) && voiceId.Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        if (provider == TtsProvider.KittenTts && !Guid.TryParse(voiceId, out _)) return null;
        if (!Guid.TryParse(voiceId, out var id))
        {
            throw new ApiException(ErrorCodes.BadRequest, $"Invalid recorded voice ID: {voiceId}");
        }

        var path = Path.Combine(storage.Value.DataFolder, "voices", $"{id}.wav");
        if (!File.Exists(path))
        {
            throw new ApiException(ErrorCodes.InvalidFile, $"Voice sample file was not found for voice ID: {voiceId}");
        }
        var metadata = await database.Voices.AsNoTracking()
            .Where(voice => voice.Id == id)
            .Select(voice => new { voice.Language, voice.Transcript })
            .FirstOrDefaultAsync(cancellationToken);
        if (metadata is null)
        {
            throw new ApiException(ErrorCodes.BadRequest, $"Recorded voice {voiceId} is unavailable.");
        }
        var audio = await File.ReadAllBytesAsync(path, cancellationToken);
        var audioHash = SHA256.HashData(audio);
        if (provider == TtsProvider.KittenTts && string.IsNullOrWhiteSpace(metadata.Transcript))
            throw new ApiException(ErrorCodes.BadRequest, "KittenTTS recorded voices require an exact sample transcript.");
        var consumedMetadata = provider == TtsProvider.KittenTts ? metadata.Transcript!.Trim() : provider == TtsProvider.Audio8
            ? $"{metadata.Language}\n{metadata.Transcript}"
            : metadata.Language.ToString();
        var revisionBytes = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"recorded-voice-v1\n{provider}\n{Convert.ToHexString(audioHash)}\n{consumedMetadata}"));
        return new RecordedTtsVoice(audio, metadata.Language, metadata.Transcript, Convert.ToHexString(revisionBytes));
    }
}
