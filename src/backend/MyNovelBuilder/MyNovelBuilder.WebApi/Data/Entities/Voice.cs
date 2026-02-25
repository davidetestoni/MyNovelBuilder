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
}