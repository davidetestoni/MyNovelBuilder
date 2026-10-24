using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Models.Tts;

/// <summary>Sound-affecting values resolved before a shared TTS generation call.</summary>
public sealed record ResolvedTtsGenerationOptions(
    TtsProvider Provider,
    string ModelId,
    string VoiceId,
    TextGenerationProvider TextGenerationProvider,
    string TextGenerationModelId,
    bool EnableTextEmphasis,
    string? EndpointIdentity,
    string? VoiceRevision);
