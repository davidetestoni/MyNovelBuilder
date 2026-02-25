namespace MyNovelBuilder.WebApi.Dtos.Generate;

using FluentValidation;
using MyNovelBuilder.WebApi.Enums;

/// <summary>
/// DTO for a Text-to-Speech request.
/// </summary>
public class TtsRequestDto
{
    /// <summary>
    /// The message to generate audio for.
    /// </summary>
    public required string Message { get; set; }
    
    /// <summary>
    /// Optional voice ID override for this request.
    /// If null or empty, the configured integrations voice is used.
    /// </summary>
    public string? VoiceId { get; set; }
    
    /// <summary>
    /// Optional TTS provider override for this request.
    /// If null, the configured integrations provider is used.
    /// </summary>
    public TtsProvider? Provider { get; set; }
}

internal class TtsRequestDtoValidator : AbstractValidator<TtsRequestDto>
{
    public TtsRequestDtoValidator()
    {
        RuleFor(x => x.Message).NotEmpty().MaximumLength(50_000);
        RuleFor(x => x.VoiceId).MaximumLength(200);
        RuleFor(x => x.Provider)
            .Must(v => !v.HasValue || Enum.IsDefined(v.Value))
            .WithMessage("TTS provider is invalid.");
    }
}
