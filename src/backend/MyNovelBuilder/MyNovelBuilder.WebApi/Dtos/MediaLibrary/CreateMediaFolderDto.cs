using FluentValidation;

namespace MyNovelBuilder.WebApi.Dtos.MediaLibrary;

/// <summary>
/// Data transfer object for linking a media folder.
/// </summary>
public class CreateMediaFolderDto
{
    /// <summary>
    /// The display name of the folder.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The local filesystem path of the folder.
    /// </summary>
    public required string Path { get; set; }
}

internal class CreateMediaFolderDtoValidator : AbstractValidator<CreateMediaFolderDto>
{
    public CreateMediaFolderDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Path).NotEmpty().MaximumLength(1000);
    }
}
