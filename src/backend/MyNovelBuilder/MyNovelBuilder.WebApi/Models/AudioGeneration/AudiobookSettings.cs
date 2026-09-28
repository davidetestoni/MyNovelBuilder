using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Models.Tts;

namespace MyNovelBuilder.WebApi.Models.AudioGeneration;

/// <summary>Optional settings for one export. Null means use the current default.</summary>
public sealed record AudiobookSettingsRequest(
    TtsProvider? Provider = null,
    string? ModelId = null,
    string? VoiceId = null,
    bool? EnableTextEmphasis = null,
    bool? EnableImmersive = null,
    int? ImmersivePauseMs = null,
    TextGenerationProvider? TextGenerationProvider = null,
    string? TextGenerationModelId = null,
    Guid? ImmersivePromptId = null);

/// <summary>All settings that must remain fixed throughout an audiobook run.</summary>
public sealed record ResolvedAudiobookSettings(
    TtsProvider Provider,
    string ModelId,
    string VoiceId,
    bool EnableTextEmphasis,
    bool EnableImmersive,
    int ImmersivePauseMs,
    TextGenerationProvider TextGenerationProvider,
    string TextGenerationModelId,
    Guid? ImmersivePromptId,
    string? TtsEndpointIdentity,
    string? NarratorVoiceRevision)
{
    /// <summary>Builds generic TTS options for the narrator or a frozen character voice.</summary>
    public ResolvedTtsGenerationOptions ToTtsOptions(
        string? voiceId = null,
        string? voiceRevision = null) => new(
        Provider,
        ModelId,
        voiceId ?? VoiceId,
        TextGenerationProvider,
        TextGenerationModelId,
        EnableTextEmphasis,
        TtsEndpointIdentity,
        voiceRevision ?? (voiceId is null || voiceId == VoiceId ? NarratorVoiceRevision : null));
}
