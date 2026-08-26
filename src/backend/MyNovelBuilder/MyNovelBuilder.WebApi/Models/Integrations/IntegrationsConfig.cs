using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Models.Integrations;

/// <summary>
/// Configuration for integrations.
/// </summary>
public class IntegrationsConfig
{
    /// <summary>
    /// The default text generation model ID to use for new or incomplete integrations config.
    /// </summary>
    public const string DefaultTextGenerationModelId = "openrouter/auto";

    /// <summary>
    /// The default base URL for the custom TTS provider.
    /// </summary>
    public const string DefaultCustomTtsBaseUrl = "http://localhost:5000/";

    /// <summary>
    /// The default base URL for the Pocket TTS provider.
    /// </summary>
    public const string DefaultPocketTtsBaseUrl = "http://localhost:8000/";

    /// <summary>
    /// The default base URL for the VibeVoice provider.
    /// </summary>
    public const string DefaultVibeVoiceBaseUrl = "http://localhost:8000/";

    /// <summary>
    /// The default base URL for the Chatterbox provider.
    /// </summary>
    public const string DefaultChatterboxBaseUrl = "http://localhost:8000/";

    /// <summary>
    /// The default base URL for the Qwen3 provider.
    /// </summary>
    public const string DefaultQwen3BaseUrl = "http://localhost:8000/";

    /// <summary>
    /// The default base URL for the OmniVoice provider.
    /// </summary>
    public const string DefaultOmniVoiceBaseUrl = "http://localhost:8000/";

    /// <summary>
    /// The default base URL for the Audio8 provider.
    /// </summary>
    public const string DefaultAudio8BaseUrl = "http://localhost:8000/";

    /// <summary>
    /// The OpenRouter API key.
    /// </summary>
    public string? OpenRouterApiKey { get; set; }
    
    /// <summary>
    /// The Google GenAI API key.
    /// </summary>
    public string? GoogleGenAiApiKey { get; set; }
    
    /// <summary>
    /// The ElevenLabs API key.
    /// </summary>
    public string? ElevenLabsApiKey { get; set; }
    
    /// <summary>
    /// The UnrealSpeech API key.
    /// </summary>
    public string? UnrealSpeechApiKey { get; set; }

    /// <summary>
    /// The DeAPI API key.
    /// </summary>
    public string? DeApiApiKey { get; set; }
    
    /// <summary>
    /// The NanoGPT API key.
    /// </summary>
    public string? NanoGptApiKey { get; set; }

    /// <summary>
    /// The base URL for the custom TTS provider.
    /// </summary>
    public string CustomTtsBaseUrl { get; set; } = DefaultCustomTtsBaseUrl;

    /// <summary>
    /// The base URL for the Pocket TTS provider.
    /// </summary>
    public string PocketTtsBaseUrl { get; set; } = DefaultPocketTtsBaseUrl;

    /// <summary>
    /// The base URL for the VibeVoice provider.
    /// </summary>
    public string VibeVoiceBaseUrl { get; set; } = DefaultVibeVoiceBaseUrl;

    /// <summary>
    /// The base URL for the Chatterbox provider.
    /// </summary>
    public string ChatterboxBaseUrl { get; set; } = DefaultChatterboxBaseUrl;

    /// <summary>
    /// The base URL for the Qwen3 provider.
    /// </summary>
    public string Qwen3BaseUrl { get; set; } = DefaultQwen3BaseUrl;

    /// <summary>
    /// The base URL for the OmniVoice provider.
    /// </summary>
    public string OmniVoiceBaseUrl { get; set; } = DefaultOmniVoiceBaseUrl;

    /// <summary>
    /// The base URL for the Audio8 provider.
    /// </summary>
    public string Audio8BaseUrl { get; set; } = DefaultAudio8BaseUrl;
    
    /// <summary>
    /// The Text Generation provider to use to generate text.
    /// </summary>
    public TextGenerationProvider TextGenerationProvider { get; set; } = TextGenerationProvider.OpenRouter;

    /// <summary>
    /// The default text generation model ID to use where a model is required but not explicitly selected.
    /// </summary>
    public string TextGenerationModelId { get; set; } = DefaultTextGenerationModelId;

    /// <summary>
    /// The Text-to-Speech provider to use to generate speech.
    /// </summary>
    public TtsProvider TtsProvider { get; set; } = TtsProvider.ElevenLabs;

    /// <summary>
    /// The Image Generation provider to use to generate images.
    /// </summary>
    public ImageGenerationProvider ImageGenerationProvider { get; set; } = ImageGenerationProvider.DeApi;

    /// <summary>
    /// The Video Generation provider to use to generate videos.
    /// </summary>
    public VideoGenerationProvider VideoGenerationProvider { get; set; } = VideoGenerationProvider.DeApi;
    
    /// <summary>
    /// The TTS model ID to use for text-to-speech generation.
    /// </summary>
    public string TtsModelId { get; set; } = string.Empty;

    /// <summary>
    /// The TTS voice ID to use for text-to-speech generation.
    /// </summary>
    public string TtsVoiceId { get; set; } = string.Empty;

    /// <summary>
    /// Whether text should be emphasized with speech tags before TTS generation.
    /// </summary>
    public bool TtsEnableTextEmphasis { get; set; }

    /// <summary>
    /// Whether immersive multi-speaker TTS playback should be used when available.
    /// </summary>
    public bool TtsEnableImmersive { get; set; }

    /// <summary>
    /// Global pause in milliseconds between immersive TTS chunks.
    /// </summary>
    public int TtsImmersivePauseMs { get; set; } = 150;

    internal IntegrationsConfig Copy() => (IntegrationsConfig)MemberwiseClone();
}
