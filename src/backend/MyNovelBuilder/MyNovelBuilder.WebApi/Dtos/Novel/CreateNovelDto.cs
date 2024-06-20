using FluentValidation;
using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Dtos.Novel;

/// <summary>
/// Data transfer object for creating a novel.
/// </summary>
public class CreateNovelDto
{
    /// <summary>
    /// The novel's title.
    /// </summary>
    public required string Title { get; set; }
    
    /// <summary>
    /// The novel's author.
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// A brief description of the novel.
    /// </summary>
    public string Brief { get; set; } = string.Empty;

    /// <summary>
    /// The novel's writing tense.
    /// </summary>
    public WritingTense Tense { get; set; } = WritingTense.Present;
}

internal class CreateNovelDtoValidator : AbstractValidator<CreateNovelDto>
{
    public CreateNovelDtoValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Brief).MaximumLength(500);
        RuleFor(x => x.Tense).IsInEnum();
    }
}
