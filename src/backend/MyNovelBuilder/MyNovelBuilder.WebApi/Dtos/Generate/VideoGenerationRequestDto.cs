using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FluentValidation;

namespace MyNovelBuilder.WebApi.Dtos.Generate;

/// <summary>
/// DTO for a video generation request.
/// </summary>
public class VideoGenerationRequestDto
{
    /// <summary>
    /// The model ID to use for generating the video.
    /// </summary>
    public required string ModelId { get; set; }

    /// <summary>
    /// The prompt to generate the video from.
    /// </summary>
    public required string Prompt { get; set; }

    /// <summary>
    /// The width of the video to generate, in pixels.
    /// </summary>
    [JsonRequired]
    [Range(1, 10_000)]
    public int Width { get; set; }

    /// <summary>
    /// The height of the video to generate, in pixels.
    /// </summary>
    [JsonRequired]
    [Range(1, 10_000)]
    public int Height { get; set; }
}

internal class VideoGenerationRequestDtoValidator : AbstractValidator<VideoGenerationRequestDto>
{
    public VideoGenerationRequestDtoValidator()
    {
        RuleFor(x => x.ModelId).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Prompt).NotEmpty().MaximumLength(20_000);
        RuleFor(x => x.Width).InclusiveBetween(1, 10_000);
        RuleFor(x => x.Height).InclusiveBetween(1, 10_000);
    }
}
