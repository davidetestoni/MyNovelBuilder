using FluentValidation;
using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Dtos.Prompt;

/// <summary>
/// Data transfer object for a prompt message.
/// </summary>
public class PromptMessageDto
{
    /// <summary>
    /// The role of the prompt message.
    /// </summary>
    public required PromptMessageRole Role { get; set; }
    
    /// <summary>
    /// The message.
    /// </summary>
    public required string Message { get; set; }
}

internal class PromptMessageDtoValidator : AbstractValidator<PromptMessageDto>
{
    public PromptMessageDtoValidator()
    {
        RuleFor(x => x.Role).IsInEnum();
        RuleFor(x => x.Message).MaximumLength(50000);
    }
}
