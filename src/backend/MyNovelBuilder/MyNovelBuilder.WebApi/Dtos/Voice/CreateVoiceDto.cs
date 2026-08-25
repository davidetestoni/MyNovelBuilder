using FluentValidation;
using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Dtos.Voice;

/// <summary>
/// Data transfer object for creating a voice.
/// </summary>
public class CreateVoiceDto
{
    /// <summary>
    /// The voice's name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The voice's gender.
    /// </summary>
    public required VoiceGender VoiceGender { get; set; }

    /// <summary>
    /// The voice's language.
    /// </summary>
    public WritingLanguage Language { get; set; } = WritingLanguage.English;

    /// <summary>
    /// The exact transcript of the voice sample.
    /// </summary>
    public string? Transcript { get; set; }

    /// <summary>
    /// The voice sample WAV file.
    /// </summary>
    public required IFormFile File { get; set; }
}

internal class CreateVoiceDtoValidator : AbstractValidator<CreateVoiceDto>
{
    public CreateVoiceDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.VoiceGender).IsInEnum();
        RuleFor(x => x.Language).IsInEnum();
        RuleFor(x => x.Transcript).MaximumLength(50000);
        RuleFor(x => x.File)
            .NotNull()
            .Must(file => string.Equals(Path.GetExtension(file.FileName), ".wav", StringComparison.OrdinalIgnoreCase))
            .WithMessage("The voice sample file must be a .wav file.");
    }
}
