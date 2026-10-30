using FluentValidation;
using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Dtos.CompendiumRecord;

/// <summary>
/// DTO for a character voice assignment.
/// </summary>
public class CharacterVoiceAssignmentDto
{
    /// <summary>Fixed playback gain in decibels; zero leaves the source level unchanged.</summary>
    public double GainDb { get; set; }

    /// <summary>
    /// The TTS provider this assignment applies to.
    /// </summary>
    public required TtsProvider Provider { get; set; }

    /// <summary>
    /// The TTS model ID this assignment applies to.
    /// </summary>
    public required string ModelId { get; set; }

    /// <summary>
    /// The provider-specific voice ID.
    /// </summary>
    public required string VoiceId { get; set; }

    /// <summary>
    /// Optional voice name snapshot for display.
    /// </summary>
    public string? VoiceName { get; set; }

    /// <summary>
    /// The time the assignment was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

internal class CharacterVoiceAssignmentDtoValidator : AbstractValidator<CharacterVoiceAssignmentDto>
{
    public CharacterVoiceAssignmentDtoValidator()
    {
        RuleFor(x => x.GainDb).InclusiveBetween(-60, 60);
        RuleFor(x => x.Provider).IsInEnum();
        RuleFor(x => x.ModelId).NotEmpty().MaximumLength(200);
        RuleFor(x => x.VoiceId).NotEmpty().MaximumLength(200);
        RuleFor(x => x.VoiceName).MaximumLength(200);
    }
}
