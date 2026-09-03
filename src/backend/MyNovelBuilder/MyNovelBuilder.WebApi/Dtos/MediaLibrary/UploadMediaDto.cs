using FluentValidation;

namespace MyNovelBuilder.WebApi.Dtos.MediaLibrary;

/// <summary>
/// Data transfer object for uploading a media file.
/// </summary>
public class UploadMediaDto
{
    /// <summary>
    /// The desired file name, including extension.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The media file bytes.
    /// </summary>
    public required IFormFile File { get; set; }
}

internal class UploadMediaDtoValidator : AbstractValidator<UploadMediaDto>
{
    private static readonly string[] AllowedExtensions = [".png", ".jpg", ".jpeg", ".webp", ".gif", ".mp4"];

    public UploadMediaDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(255)
            .Must(HaveAllowedExtension)
            .WithMessage("The media file name must use one of these extensions: .png, .jpg, .jpeg, .webp, .gif, .mp4.");
        RuleFor(x => x.File).NotNull();
    }

    private static bool HaveAllowedExtension(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }
}
