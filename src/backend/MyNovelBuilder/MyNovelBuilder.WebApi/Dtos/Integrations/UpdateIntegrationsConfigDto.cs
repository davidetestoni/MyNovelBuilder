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
    /// The base URL for the custom TTS provider.
    /// </summary>
    public string? CustomTtsBaseUrl { get; init; }

    /// <summary>
    /// The base URL for the Pocket TTS provider.
    /// </summary>
    public string? PocketTtsBaseUrl { get; init; }

    /// <summary>
    /// The base URL for the VibeVoice provider.
    /// </summary>
    public string? VibeVoiceBaseUrl { get; init; }

    /// <summary>
    /// The base URL for the Chatterbox provider.
    /// </summary>
    public string? ChatterboxBaseUrl { get; init; }

    /// <summary>
    /// The base URL for the Qwen3 provider.
    /// </summary>
    public string? Qwen3BaseUrl { get; init; }

    /// <summary>
    /// The base URL for the OmniVoice provider.
    /// </summary>
    public string? OmniVoiceBaseUrl { get; init; }

    /// <summary>
    /// The base URL for the Audio8 provider.
    /// </summary>
    public string? Audio8BaseUrl { get; init; }
    
    /// <summary>
    /// The Text Generation provider to use to generate text.
    /// </summary>
    public TextGenerationProvider? TextGenerationProvider { get; init; }

    /// <summary>
    /// The default text generation model ID.
    /// </summary>
    public string? TextGenerationModelId { get; init; }
    
    /// <summary>
    /// The Text-to-Speech provider to use to generate speech.
    /// </summary>
    public TtsProvider? TtsProvider { get; init; }

    /// <summary>
    /// The Image Generation provider to use to generate images.
    /// </summary>
    public ImageGenerationProvider? ImageGenerationProvider { get; init; }

    /// <summary>
    /// The Video Generation provider to use to generate videos.
    /// </summary>
    public VideoGenerationProvider? VideoGenerationProvider { get; init; }
    
    /// <summary>
    /// The TTS model ID to use for text-to-speech generation.
    /// </summary>
    public string? TtsModelId { get; init; }

    /// <summary>
    /// The TTS voice ID to use for text-to-speech generation.
    /// </summary>
    public string? TtsVoiceId { get; init; }

    /// <summary>
    /// Whether text emphasis with speech tags should be enabled for TTS generation.
    /// </summary>
    public bool? TtsEnableTextEmphasis { get; init; }

    /// <summary>
    /// Whether immersive multi-speaker TTS playback should be enabled when available.
    /// </summary>
    public bool? TtsEnableImmersive { get; init; }

    /// <summary>
    /// Global pause in milliseconds between immersive TTS chunks.
    /// </summary>
    public int? TtsImmersivePauseMs { get; init; }
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
        RuleFor(x => x.CustomTtsBaseUrl).MaximumLength(2000).Must(BeValidHttpBaseUrl)
            .WithMessage("Custom TTS base URL must be a valid absolute HTTP or HTTPS URL.");
        RuleFor(x => x.PocketTtsBaseUrl).MaximumLength(2000).Must(BeValidHttpBaseUrl)
            .WithMessage("Pocket TTS base URL must be a valid absolute HTTP or HTTPS URL.");
        RuleFor(x => x.VibeVoiceBaseUrl).MaximumLength(2000).Must(BeValidHttpBaseUrl)
            .WithMessage("VibeVoice base URL must be a valid absolute HTTP or HTTPS URL.");
        RuleFor(x => x.ChatterboxBaseUrl).MaximumLength(2000).Must(BeValidHttpBaseUrl)
            .WithMessage("Chatterbox base URL must be a valid absolute HTTP or HTTPS URL.");
        RuleFor(x => x.Qwen3BaseUrl).MaximumLength(2000).Must(BeValidHttpBaseUrl)
            .WithMessage("Qwen3 base URL must be a valid absolute HTTP or HTTPS URL.");
        RuleFor(x => x.OmniVoiceBaseUrl).MaximumLength(2000).Must(BeValidHttpBaseUrl)
            .WithMessage("OmniVoice base URL must be a valid absolute HTTP or HTTPS URL.");
        RuleFor(x => x.Audio8BaseUrl).MaximumLength(2000).Must(BeValidHttpBaseUrl)
            .WithMessage("Audio8 base URL must be a valid absolute HTTP or HTTPS URL.");
        RuleFor(x => x.TextGenerationModelId).MaximumLength(200);
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
        RuleFor(x => x.VideoGenerationProvider)
            .Must(v => !v.HasValue || Enum.IsDefined(v.Value))
            .WithMessage("Video generation provider is invalid.");

        RuleFor(x => x.TtsImmersivePauseMs)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(10_000)
            .When(x => x.TtsImmersivePauseMs.HasValue);
    }

    private static bool BeValidHttpBaseUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
