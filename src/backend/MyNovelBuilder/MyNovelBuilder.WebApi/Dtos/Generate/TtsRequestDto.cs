namespace MyNovelBuilder.WebApi.Dtos.Generate;

using FluentValidation;
using MyNovelBuilder.WebApi.Enums;

/// <summary>
/// DTO for a Text-to-Speech request.
/// </summary>
public class TtsRequestDto
{
    /// <summary>Fixed playback gain in decibels; zero leaves the source level unchanged.</summary>
    public double? GainDb { get; set; }

    /// <summary>
    /// The message to generate audio for.
    /// </summary>
    public required string Message { get; set; }
    
    /// <summary>
    /// Optional model ID override for this request.
    /// If null or empty, the configured integrations model is used.
    /// </summary>
    public string? ModelId { get; set; }

    /// <summary>
    /// Optional voice ID override for this request.
    /// If null or empty, the configured integrations voice is used.
    /// </summary>
    public string? VoiceId { get; set; }
    
    /// <summary>
    /// Optional TTS provider override for this request.
    /// If null, the configured integrations provider is used.
    /// </summary>
    public TtsProvider? Provider { get; set; }
}

internal class TtsRequestDtoValidator : AbstractValidator<TtsRequestDto>
{
    public TtsRequestDtoValidator()
    {
        RuleFor(x => x.GainDb).InclusiveBetween(-60, 60);
        RuleFor(x => x.Message).NotEmpty().MaximumLength(50_000);
        RuleFor(x => x.ModelId).MaximumLength(200);
        RuleFor(x => x.VoiceId).MaximumLength(200);
        RuleFor(x => x.Provider)
            .Must(v => !v.HasValue || Enum.IsDefined(v.Value))
            .WithMessage("TTS provider is invalid.");
    }
}
