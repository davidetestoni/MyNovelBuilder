using FluentValidation;
using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Dtos.Generate;

/// <summary>
/// Request for immersive multi-speaker TTS playback for a prose section.
/// </summary>
public class ImmersiveTtsRequestDto
{
    /// <summary>
    /// The novel containing the prose section to narrate.
    /// </summary>
    public required Guid NovelId { get; set; }

    /// <summary>
    /// The prompt used to plan the speaker chunks.
    /// </summary>
    public Guid PromptId { get; set; }

    /// <summary>
    /// The chapter index to narrate.
    /// </summary>
    public int ChapterIndex { get; set; }

    /// <summary>
    /// The section index to narrate.
    /// </summary>
    public int SectionIndex { get; set; }

    /// <summary>
    /// Optional TTS provider override.
    /// </summary>
    public TtsProvider? Provider { get; set; }

    /// <summary>
    /// Optional TTS model override.
    /// </summary>
    public string? TtsModelId { get; set; }

    /// <summary>
    /// Optional narrator voice override.
    /// </summary>
    public string? VoiceId { get; set; }

    /// <summary>
    /// Optional text-generation model override for chunk planning and emphasis.
    /// </summary>
    public string? TextGenerationModelId { get; set; }
}

internal class ImmersiveTtsRequestDtoValidator : AbstractValidator<ImmersiveTtsRequestDto>
{
    public ImmersiveTtsRequestDtoValidator()
    {
        RuleFor(x => x.NovelId).NotEmpty();
        RuleFor(x => x.PromptId).NotEmpty();
        RuleFor(x => x.ChapterIndex).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SectionIndex).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TtsModelId).MaximumLength(200);
        RuleFor(x => x.VoiceId).MaximumLength(200);
        RuleFor(x => x.TextGenerationModelId).MaximumLength(200);
        RuleFor(x => x.Provider)
            .Must(v => !v.HasValue || Enum.IsDefined(v.Value))
            .WithMessage("TTS provider is invalid.");
    }
}
