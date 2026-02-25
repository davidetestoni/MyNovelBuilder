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
    /// The voice sample WAV file.
    /// </summary>
    public required IFormFile File { get; set; }
}

internal class UpdateVoiceDtoValidator : AbstractValidator<UpdateVoiceDto>
{
    public UpdateVoiceDtoValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.VoiceGender).IsInEnum();
        RuleFor(x => x.File)
            .NotNull()
            .Must(file => string.Equals(Path.GetExtension(file.FileName), ".wav", StringComparison.OrdinalIgnoreCase))
            .WithMessage("The voice sample file must be a .wav file.");
    }
}
