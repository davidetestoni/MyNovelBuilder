using FluentValidation;
using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Dtos.Integrations;

/// <summary>
/// DTO for updating integrations configuration.
/// </summary>
public class UpdateIntegrationsConfigDto
{
    /// <summary>
    /// The OpenRouter API key.
    /// </summary>
    public string? OpenRouterApiKey { get; init; }
    
    /// <summary>
    /// The Google GenAI API key.
    /// </summary>
    public string? GoogleGenAiApiKey { get; init; }
    
    /// <summary>
    /// The ElevenLabs API key.
    /// </summary>
    public string? ElevenLabsApiKey { get; set; }
    
    /// <summary>
    /// The UnrealSpeech API key.
    /// </summary>
    public string? UnrealSpeechApiKey { get; set; }

    /// <summary>
    /// The DeAPI API key.
    /// </summary>
    public string? DeApiApiKey { get; init; }

    /// <summary>
    /// The NanoGPT API key.
    /// </summary>
    public string? NanoGptApiKey { get; init; }
    
    /// <summary>
    /// The Text Generation provider to use to generate text.
    /// </summary>
    public TextGenerationProvider? TextGenerationProvider { get; init; }
    
    /// <summary>
    /// The Text-to-Speech provider to use to generate speech.
    /// </summary>
    public TtsProvider? TtsProvider { get; init; }

    /// <summary>
    /// The Image Generation provider to use to generate images.
    /// </summary>
    public ImageGenerationProvider? ImageGenerationProvider { get; init; }
    
    /// <summary>
    /// The TTS model ID to use for text-to-speech generation.
    /// </summary>
    public string? TtsModelId { get; init; }

    /// <summary>
    /// The TTS voice ID to use for text-to-speech generation.
    /// </summary>
    public string? TtsVoiceId { get; init; }
}

internal class UpdateIntegrationsConfigDtoValidator : AbstractValidator<UpdateIntegrationsConfigDto>
{
    public UpdateIntegrationsConfigDtoValidator()
    {
        RuleFor(x => x.OpenRouterApiKey).MaximumLength(500);
        RuleFor(x => x.GoogleGenAiApiKey).MaximumLength(500);
        RuleFor(x => x.ElevenLabsApiKey).MaximumLength(500);
        RuleFor(x => x.UnrealSpeechApiKey).MaximumLength(500);
        RuleFor(x => x.DeApiApiKey).MaximumLength(500);
        RuleFor(x => x.NanoGptApiKey).MaximumLength(500);
        RuleFor(x => x.TtsModelId).MaximumLength(200);
        RuleFor(x => x.TtsVoiceId).MaximumLength(200);

        RuleFor(x => x.TextGenerationProvider)
            .Must(v => !v.HasValue || Enum.IsDefined(v.Value))
            .WithMessage("Text generation provider is invalid.");
        RuleFor(x => x.TtsProvider)
            .Must(v => !v.HasValue || Enum.IsDefined(v.Value))
            .WithMessage("TTS provider is invalid.");
        RuleFor(x => x.ImageGenerationProvider)
            .Must(v => !v.HasValue || Enum.IsDefined(v.Value))
            .WithMessage("Image generation provider is invalid.");
    }
}
