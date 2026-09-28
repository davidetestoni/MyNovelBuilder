using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Helpers;
using MyNovelBuilder.WebApi.Models.AudioGeneration;
using MyNovelBuilder.WebApi.Services.Tts;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>Resolves and validates one export's sound settings without changing global defaults.</summary>
public sealed class AudiobookSettingsResolver(
    IIntegrationsService integrations,
    IServiceProvider services,
    ITtsVoiceRevisionService voiceRevisions)
{
    public async Task<ResolvedAudiobookSettings> ResolveAsync(
        AudiobookSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var defaults = await integrations.GetConfigAsync(cancellationToken);
        var provider = request.Provider ?? defaults.TtsProvider;
        var modelId = request.ModelId ?? defaults.TtsModelId;
        var voiceId = request.VoiceId ?? defaults.TtsVoiceId;
        var textProvider = request.TextGenerationProvider ?? defaults.TextGenerationProvider;
        var textModelId = request.TextGenerationModelId ?? defaults.TextGenerationModelId;
        var pauseMs = request.ImmersivePauseMs ?? defaults.TtsImmersivePauseMs;
        var immersive = request.EnableImmersive ?? defaults.TtsEnableImmersive;

        if (!Enum.IsDefined(provider) || services.GetKeyedService<ITtsService>(provider) is not { } tts)
        {
            throw BadRequest("The selected TTS provider is unavailable.");
        }
        if (pauseMs is < 0 or > 10000)
        {
            throw BadRequest("Immersive pause must be between 0 and 10000 milliseconds.");
        }
        if (string.IsNullOrWhiteSpace(modelId) || string.IsNullOrWhiteSpace(voiceId))
        {
            throw BadRequest("Select a TTS model and narrator voice before generating an audiobook.");
        }
        // Changing providers never carries an unvalidated model or voice across providers.
        var models = (await tts.GetModelsAsync(cancellationToken)).ToList();
        var model = models.FirstOrDefault(m => string.Equals(m.ModelId, modelId, StringComparison.Ordinal));
        if (model is null)
        {
            throw BadRequest($"TTS model '{modelId}' is not available for {provider}.");
        }
        if (!model.Voices.Any(v => string.Equals(v.VoiceId, voiceId, StringComparison.Ordinal)))
        {
            throw BadRequest($"Narrator voice '{voiceId}' is not available for model '{modelId}'.");
        }

        var emphasis = request.EnableTextEmphasis ?? defaults.TtsEnableTextEmphasis;
        if (emphasis && !tts.SupportsTextEmphasis(modelId))
        {
            throw BadRequest($"Text emphasis is not supported by model '{modelId}'.");
        }
        if (immersive && request.ImmersivePromptId is null)
        {
            throw BadRequest("Select an immersive planning prompt before generating an immersive audiobook.");
        }

        if (immersive || emphasis)
        {
            if (!Enum.IsDefined(textProvider)
                || services.GetKeyedService<TextGeneration.ITextGenerationService>(textProvider) is not { } textService)
            {
                throw BadRequest("The selected text-generation provider is unavailable.");
            }
            var textModels = await textService.GetAvailableModelsAsync(cancellationToken);
            if (!textModels.Any(model => string.Equals(model.Id, textModelId, StringComparison.Ordinal)))
            {
                throw BadRequest($"Text-generation model '{textModelId}' is not available for {textProvider}.");
            }
        }

        return new ResolvedAudiobookSettings(
            provider, modelId, voiceId, emphasis, immersive, pauseMs,
            textProvider, textModelId, request.ImmersivePromptId,
            TtsEndpointIdentity.FromConfig(defaults, provider),
            await voiceRevisions.GetRevisionAsync(
                provider, voiceId, cancellationToken));
    }

    private static ApiException BadRequest(string message) => new(ErrorCodes.BadRequest, message);
}
