using FluentValidation;
using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Dtos.Voice;

/// <summary>
/// Data transfer object for updating a voice.
/// </summary>
public class UpdateVoiceDto
{
    /// <summary>
    /// The voice's ID.
    /// </summary>
    public required Guid Id { get; set; }

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
    /// An optional replacement voice sample WAV file.
    /// </summary>
    public IFormFile? File { get; set; }
}

internal class UpdateVoiceDtoValidator : AbstractValidator<UpdateVoiceDto>
{
    public UpdateVoiceDtoValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.VoiceGender).IsInEnum();
        RuleFor(x => x.Language).IsInEnum();
        RuleFor(x => x.Transcript).MaximumLength(50000);
        RuleFor(x => x.File)
            .Must(file => file is null
                          || string.Equals(
                              Path.GetExtension(file.FileName),
                              ".wav",
                              StringComparison.OrdinalIgnoreCase))
            .WithMessage("The voice sample file must be a .wav file.");
    }
}
