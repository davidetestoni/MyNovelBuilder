namespace MyNovelBuilder.WebApi.Dtos.Generate;

using FluentValidation;

/// <summary>
/// DTO for a Text-to-Speech request.
/// </summary>
public class TtsRequestDto
{
    /// <summary>
    /// The message to generate audio for.
    /// </summary>
    public required string Message { get; set; }
}

internal class TtsRequestDtoValidator : AbstractValidator<TtsRequestDto>
{
    public TtsRequestDtoValidator()
    {
        RuleFor(x => x.Message).NotEmpty().MaximumLength(50_000);
    }
}
