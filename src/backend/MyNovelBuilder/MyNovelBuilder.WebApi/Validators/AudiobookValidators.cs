using FluentValidation;
using MyNovelBuilder.WebApi.Models.AudioGeneration;

namespace MyNovelBuilder.WebApi.Validators;

/// <summary>Validates provider selections, pause duration and setting identifier lengths before preview creation.</summary>
public sealed class AudiobookSettingsRequestValidator : AbstractValidator<AudiobookSettingsRequest>
{
    /// <summary>Defines the accepted audiobook setting ranges.</summary>
    public AudiobookSettingsRequestValidator()
    {
        RuleFor(x => x.Provider).IsInEnum();
        RuleFor(x => x.TextGenerationProvider).IsInEnum();
        RuleFor(x => x.ImmersivePauseMs).InclusiveBetween(0, 10000);
        RuleFor(x => x.ModelId).MaximumLength(512);
        RuleFor(x => x.VoiceId).MaximumLength(512);
        RuleFor(x => x.TextGenerationModelId).MaximumLength(512);
    }
}
/// <summary>Requires a preview identifier and a bounded submission key when starting a job.</summary>
public sealed class StartAudiobookJobRequestValidator : AbstractValidator<StartAudiobookJobRequest>
{
    /// <summary>Defines required submission fields and the idempotency key length limit.</summary>
    public StartAudiobookJobRequestValidator()
    {
        RuleFor(x => x.SnapshotId).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
        RuleFor(x => x.MaxParallelSections).InclusiveBetween(1, 8);
    }
}
/// <summary>Validates optional execution settings when resuming a saved job.</summary>
public sealed class RetryAudiobookJobRequestValidator : AbstractValidator<RetryAudiobookJobRequest>
{
    /// <summary>Limits concurrency while allowing omitted values to retain the saved selection.</summary>
    public RetryAudiobookJobRequestValidator()
    {
        RuleFor(x => x.MaxParallelSections).InclusiveBetween(1, 8);
    }
}
