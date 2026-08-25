using System.ComponentModel.DataAnnotations;
using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Data.Entities;

/// <summary>
/// A voice.
/// </summary>
public class Voice : TimestampedEntity
{
    /// <summary>
    /// The voice's name.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public required string Name { get; init; }
    
    /// <summary>
    /// The voice's gender.
    /// </summary>
    public required VoiceGender VoiceGender { get; init; } = VoiceGender.Both;

    /// <summary>
    /// The language for this voice.
    /// </summary>
    public WritingLanguage Language { get; init; } = WritingLanguage.English;

    /// <summary>
    /// The exact transcript of the voice sample, when available.
    /// </summary>
    [MaxLength(50000)]
    public string? Transcript { get; init; }
}
