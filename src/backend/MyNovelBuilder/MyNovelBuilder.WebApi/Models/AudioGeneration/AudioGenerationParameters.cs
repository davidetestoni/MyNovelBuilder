using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Models.AudioGeneration;

/// <summary>
/// Model representing parameters for audio generation.
/// </summary>
public class AudioGenerationParameters
{
    /// <summary>
    /// The text to be converted to audio.
    /// </summary>
    public required string Text { get; set; }
    
    /// <summary>
    /// The TTS provider to use for audio generation.
    /// </summary>
    public required TtsProvider Provider { get; set; }
    
    /// <summary>
    /// The model ID to use for audio generation, if applicable.
    /// </summary>
    public string? ModelId { get; set; }

    /// <summary>
    /// The voice ID to use for audio generation.
    /// </summary>
    public required string VoiceId { get; set; }

    /// <summary>
    /// Whether text emphasis with speech tags is enabled for this generation.
    /// </summary>
    public bool EnableTextEmphasis { get; set; }

    /// <summary>
    /// Generates a hash based on the parameters to uniquely identify the generated audio.
    /// </summary>
    public string GetHash()
    {
        var payload = new
        {
            Provider,
            ModelId,
            VoiceId,
            EnableTextEmphasis,
            Text
        };
        var input = JsonSerializer.Serialize(payload);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(hashBytes);
    }
}
