namespace MyNovelBuilder.WebApi.Dtos.Generate;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FluentValidation;

/// <summary>
/// DTO for an image generation request.
/// </summary>
public class ImageGenerationRequestDto
{
    /// <summary>
    /// The model ID to use for generating the image.
    /// </summary>
    public required string ModelId { get; set; }
    
    /// <summary>
    /// The prompt to generate the image from.
    /// </summary>
    public required string Prompt { get; set; }
    
    /// <summary>
    /// The width of the image to generate, in pixels.
    /// </summary>
    [JsonRequired]
    [Range(1, 10_000)]
    public int Width { get; set; }
    
    /// <summary>
    /// The height of the image to generate, in pixels.
    /// </summary>
    [JsonRequired]
    [Range(1, 10_000)]
    public int Height { get; set; }
}

internal class ImageGenerationRequestDtoValidator : AbstractValidator<ImageGenerationRequestDto>
{
    public ImageGenerationRequestDtoValidator()
    {
        RuleFor(x => x.ModelId).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Prompt).NotEmpty().MaximumLength(20_000);
        RuleFor(x => x.Width).InclusiveBetween(1, 10_000);
        RuleFor(x => x.Height).InclusiveBetween(1, 10_000);
    }
}
