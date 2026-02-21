namespace MyNovelBuilder.WebApi.Dtos.Generate;

using FluentValidation;
using System.Text.Json.Serialization;

/// <summary>
/// DTO for a request to generate text.
/// </summary>
public class GenerateTextRequestDto
{
    /// <summary>
    /// The model to use for text generation.
    /// </summary>
    public required string Model { get; set; }
    
    /// <summary>
    /// The prompt ID.
    /// </summary>
    [JsonRequired]
    public Guid PromptId { get; set; }
    
    /// <summary>
    /// The context information.
    /// </summary>
    public required TextGenerationContextInfoDto ContextInfo { get; set; }
}

internal class GenerateTextRequestDtoValidator : AbstractValidator<GenerateTextRequestDto>
{
    public GenerateTextRequestDtoValidator()
    {
        RuleFor(x => x.Model).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PromptId).NotEmpty();
        RuleFor(x => x.ContextInfo).NotNull().SetValidator(new TextGenerationContextInfoDtoValidator());
    }
}
