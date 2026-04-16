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
}
