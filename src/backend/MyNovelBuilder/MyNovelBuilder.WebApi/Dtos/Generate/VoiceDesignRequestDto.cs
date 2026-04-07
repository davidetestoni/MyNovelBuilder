using FluentValidation;
using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Dtos.Generate;

/// <summary>
/// DTO for a TTS voice design request.
/// </summary>
public class VoiceDesignRequestDto
{
    /// <summary>
    /// The TTS provider to use for this voice design request.
    /// </summary>
    public required TtsProvider Provider { get; set; }

    /// <summary>
    /// The prompt text to synthesize with the designed voice.
    /// </summary>
    public required string Prompt { get; set; }

    /// <summary>
    /// The language to use for the generated voice design sample.
    /// </summary>
    public required WritingLanguage Language { get; set; }

    /// <summary>
    /// The natural language description of the desired voice.
    /// </summary>
    public required string VoiceDescription { get; set; }
}

internal class VoiceDesignRequestDtoValidator : AbstractValidator<VoiceDesignRequestDto>
{
    public VoiceDesignRequestDtoValidator()
    {
        RuleFor(x => x.Provider)
            .Must(v => Enum.IsDefined(v))
            .WithMessage("TTS provider is invalid.");
        RuleFor(x => x.Language)
            .Must(v => Enum.IsDefined(v))
            .WithMessage("Writing language is invalid.");
        RuleFor(x => x.Prompt).NotEmpty().MaximumLength(50_000);
        RuleFor(x => x.VoiceDescription).NotEmpty().MaximumLength(2_000);
    }
}
