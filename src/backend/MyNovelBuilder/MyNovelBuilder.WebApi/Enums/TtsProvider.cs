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
}
