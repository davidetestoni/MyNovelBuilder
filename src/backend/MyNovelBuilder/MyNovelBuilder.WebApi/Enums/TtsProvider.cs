namespace MyNovelBuilder.WebApi.Enums;

/// <summary>
/// Enumeration of Text-to-Speech providers.
/// </summary>
public enum TtsProvider
{
    /// <summary>
    /// Custom TTS provider defined by the user.
    /// </summary>
    Custom,
    
    /// <summary>
    /// ElevenLabs TTS provider.
    /// </summary>
    ElevenLabs,
    
    /// <summary>
    /// Kokoro TTS provider.
    /// </summary>
    Kokoro,
    
    /// <summary>
    /// Pocket TTS provider.
    /// </summary>
    PocketTts,
    
    /// <summary>
    /// UnrealSpeech TTS provider.
    /// </summary>
    UnrealSpeech,
    
    /// <summary>
    /// VibeVoice TTS provider.
    /// </summary>
    VibeVoice,
    
    /// <summary>
    /// DeAPI TTS provider.
    /// </summary>
    DeApi,
    
    /// <summary>
    /// Chatterbox TTS provider.
    /// </summary>
    Chatterbox,

    /// <summary>
    /// Qwen3 TTS provider.
    /// </summary>
    Qwen3,

    /// <summary>
    /// OmniVoice TTS provider.
    /// </summary>
    OmniVoice,
    
    /// <summary>
    /// NanoGPT TTS provider.
    /// </summary>
    NanoGpt,

    /// <summary>
    /// OpenRouter TTS provider.
    /// </summary>
    OpenRouter,

    /// <summary>
    /// Audio8 TTS provider.
    /// </summary>
    Audio8,
}
