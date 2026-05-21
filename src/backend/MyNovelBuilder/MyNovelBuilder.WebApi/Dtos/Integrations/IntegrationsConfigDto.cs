using MyNovelBuilder.WebApi.Enums;
namespace MyNovelBuilder.WebApi.Dtos.Integrations;

/// <summary>
/// DTO for reading integrations configuration.
/// Note: This DTO does not expose sensitive information such as API keys.
/// </summary>
public class IntegrationsConfigDto
{
    /// <summary>
    /// Indicates whether an OpenRouter API key is configured.
    /// </summary>
    public required bool HasOpenRouterApiKey { get; init; }
    
    /// <summary>
    /// Indicates whether a Google GenAI API key is configured.
    /// </summary>
    public required bool HasGoogleGenAiApiKey { get; init; }
    
    /// <summary>
    /// Indicates whether an ElevenLabs API key is configured.
    /// </summary>
    public required bool HasElevenLabsApiKey { get; init; }
    
    /// <summary>
    /// Indicates whether an UnrealSpeech API key is configured.
    /// </summary>
    public required bool HasUnrealSpeechApiKey { get; init; }

    /// <summary>
    /// Indicates whether a DeAPI API key is configured.
    /// </summary>
    public required bool HasDeApiApiKey { get; init; }

    /// <summary>
    /// Indicates whether a NanoGPT API key is configured.
    /// </summary>
    public required bool HasNanoGptApiKey { get; init; }

    /// <summary>
    /// The configured base URL for the custom TTS provider.
    /// </summary>
    public required string CustomTtsBaseUrl { get; init; }

    /// <summary>
    /// The configured base URL for the Pocket TTS provider.
    /// </summary>
    public required string PocketTtsBaseUrl { get; init; }

    /// <summary>
    /// The configured base URL for the VibeVoice provider.
    /// </summary>
    public required string VibeVoiceBaseUrl { get; init; }

    /// <summary>
    /// The configured base URL for the Chatterbox provider.
    /// </summary>
    public required string ChatterboxBaseUrl { get; init; }

    /// <summary>
    /// The configured base URL for the Qwen3 provider.
    /// </summary>
    public required string Qwen3BaseUrl { get; init; }

    /// <summary>
    /// The configured base URL for the OmniVoice provider.
    /// </summary>
    public required string OmniVoiceBaseUrl { get; init; }
    
    /// <summary>
    /// The configured Text Generation provider.
    /// </summary>
    public required TextGenerationProvider TextGenerationProvider { get; init; }

    /// <summary>
    /// The default text generation model ID.
    /// </summary>
    public required string TextGenerationModelId { get; init; }

    /// <summary>
    /// The configured Text-to-Speech provider.
    /// </summary>
    public required TtsProvider TtsProvider { get; init; }

    /// <summary>
    /// The configured Image Generation provider.
    /// </summary>
    public required ImageGenerationProvider ImageGenerationProvider { get; init; }

    /// <summary>
    /// The configured Video Generation provider.
    /// </summary>
    public required VideoGenerationProvider VideoGenerationProvider { get; init; }
    
    /// <summary>
    /// The TTS model ID to use for text-to-speech generation.
    /// </summary>
    public required string TtsModelId { get; init; }

    /// <summary>
    /// The TTS voice ID to use for text-to-speech generation.
    /// </summary>
    public required string TtsVoiceId { get; init; } 

    /// <summary>
    /// Whether text emphasis with speech tags is enabled for TTS generation.
    /// </summary>
    public required bool TtsEnableTextEmphasis { get; init; }

    /// <summary>
    /// Whether immersive multi-speaker TTS playback is enabled.
    /// </summary>
    public required bool TtsEnableImmersive { get; init; }

    /// <summary>
    /// Global pause in milliseconds between immersive TTS chunks.
    /// </summary>
    public required int TtsImmersivePauseMs { get; init; }
}
