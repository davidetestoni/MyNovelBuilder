using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Dtos.Generate;

/// <summary>
/// DTO describing a TTS provider and its supported features.
/// </summary>
public class TtsProviderDto
{
    /// <summary>
    /// The TTS provider identifier.
    /// </summary>
    public required TtsProvider Provider { get; init; }

    /// <summary>
    /// Indicates whether the provider supports voice design.
    /// </summary>
    public required bool SupportsVoiceDesign { get; init; }
}
