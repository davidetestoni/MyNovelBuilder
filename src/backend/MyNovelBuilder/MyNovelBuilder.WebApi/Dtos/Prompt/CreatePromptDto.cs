using FluentValidation;
using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Dtos.Prompt;

/// <summary>
/// Data transfer object for creating a prompt.
/// </summary>
public class CreatePromptDto
{
    /// <summary>
    /// The prompt's name.
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// The prompt type.
    /// </summary>
    public required PromptType Type { get; set; }
    
    /// <summary>
    /// The prompt's messages.
    /// </summary>
    public required IEnumerable<PromptMessageDto> Messages { get; set; }   
}

internal class CreatePromptDtoValidator : AbstractValidator<CreatePromptDto>
{
    public CreatePromptDtoValidator()
    {
        RuleFor(x => x.Name).MaximumLength(100);
        RuleFor(x => x.Type).IsInEnum();
        RuleForEach(x => x.Messages).SetValidator(new PromptMessageDtoValidator());
    }
}
