namespace MyNovelBuilder.WebApi.Dtos.Generate;

using FluentValidation;
using System.Text.Json.Serialization;

/// <summary>
/// DTO for describing an image.
/// </summary>
public class DescribeImageRequestDto
{
    /// <summary>
    /// The model to use for image description.
    /// </summary>
    public required string Model { get; set; }
    
    /// <summary>
    /// The prompt ID.
    /// </summary>
    [JsonRequired]
    public Guid PromptId { get; set; }
    
    /// <summary>
    /// The compendium ID.
    /// </summary>
    [JsonRequired]
    public Guid CompendiumId { get; set; }
    
    /// <summary>
    /// Additional instructions for the image description.
    /// </summary>
    public string? Instructions { get; set; }
}

internal class DescribeImageRequestDtoValidator : AbstractValidator<DescribeImageRequestDto>
{
    public DescribeImageRequestDtoValidator()
    {
        RuleFor(x => x.Model).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PromptId).NotEmpty();
        RuleFor(x => x.CompendiumId).NotEmpty();
        RuleFor(x => x.Instructions).MaximumLength(5_000);
    }
}
