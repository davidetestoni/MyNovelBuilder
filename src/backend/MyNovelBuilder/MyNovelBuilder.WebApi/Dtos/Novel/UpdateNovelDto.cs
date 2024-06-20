using FluentValidation;
using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Dtos.Novel;

/// <summary>
/// Data transfer object for updating a novel.
/// </summary>
public class UpdateNovelDto
{
    /// <summary>
    /// The novel's ID.
    /// </summary>
    public required Guid Id { get; set; }
    
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
    
    /// <summary>
    /// The novel's writing point of view.
    /// </summary>
    public WritingPov Pov { get; set; } = WritingPov.FirstPerson;
    
    /// <summary>
    /// The novel's writing language.
    /// </summary>
    public WritingLanguage Language { get; set; } = WritingLanguage.English;
}

internal class UpdateNovelDtoValidator : AbstractValidator<UpdateNovelDto>
{
    public UpdateNovelDtoValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Brief).MaximumLength(500);
        RuleFor(x => x.Tense).IsInEnum();
        RuleFor(x => x.Pov).IsInEnum();
        RuleFor(x => x.Language).IsInEnum();
    }
}
