using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Hybrid;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Models.Tts;
using MyNovelBuilder.WebApi.Services;
using MyNovelBuilder.WebApi.Services.Tts;

namespace MyNovelBuilder.WebApi.Controllers;

/// <summary>Provides TTS catalogue, capabilities, voice design and balance endpoints.</summary>
[Route("api/generate/audio")]
[ApiController]
public class TtsProviderController(
    ILogger<TtsProviderController> logger,
    IServiceProvider serviceProvider,
    IIntegrationsService integrationsService,
    HybridCache hybridCache) : ControllerBase
{
    private async ValueTask<ITtsService> GetTtsServiceAsync(
        TtsProvider? provider = null,
        CancellationToken cancellationToken = default)
    {
        TtsProvider ttsProvider;
        
        if (provider.HasValue)
        {
            ttsProvider = provider.Value;
        }
        else
        {
            var config = await integrationsService.GetConfigAsync(cancellationToken);
            ttsProvider = config.TtsProvider;
        }
        
        var ttsService = serviceProvider.GetKeyedService<ITtsService>(ttsProvider);

        if (ttsService is null)
        {
            logger.LogError(
                "Unsupported TTS provider: {Provider}", ttsProvider);

            throw new InvalidOperationException(
                $"Unsupported TTS provider: {ttsProvider}");
        }

        return ttsService;
    }
    /// <summary>
    /// Get available TTS models.
    /// </summary>
    [HttpGet("tts/models")]
    public async Task<ActionResult<IEnumerable<TtsModelDto>>> GetModels(
        [FromQuery] TtsProvider? provider,
        CancellationToken cancellationToken = default)
    {
        var ttsService = await GetTtsServiceAsync(provider, cancellationToken);
        
        var models = await ttsService.GetModelsAsync(cancellationToken);
        var effectiveProvider = provider ?? (await integrationsService.GetConfigAsync(cancellationToken)).TtsProvider;
        var modelsWithFeatures = models.Select(model =>
        {
            model.SupportsTextEmphasis = TtsAnnotationProfile.Resolve(effectiveProvider, model.ModelId) is not null;
            return model;
        });
        
        return Ok(modelsWithFeatures);
    }

    /// <summary>
    /// Get available TTS providers with feature support metadata.
    /// </summary>
    [HttpGet("tts/providers")]
    public ActionResult<IEnumerable<TtsProviderDto>> GetProviders()
    {
        var providers = Enum.GetValues<TtsProvider>()
            .Select(provider => new TtsProviderDto
            {
                Provider = provider,
                SupportsVoiceDesign =
                    serviceProvider.GetKeyedService<ITtsService>(provider)?.SupportsVoiceDesign() ?? false
            });

        return Ok(providers);
    }

    /// <summary>
    /// Generate a voice design WAV sample using the selected or configured TTS provider.
    /// </summary>
    [HttpPost("tts/voice-design")]
    public async Task<ActionResult> VoiceDesignAsync(
        VoiceDesignRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        var effectiveProvider = dto.Provider;
        var ttsService = await GetTtsServiceAsync(effectiveProvider, cancellationToken);

        try
        {
            var wavBytes = await ttsService.VoiceDesignAsync(
                dto.Prompt,
                dto.Language,
                dto.VoiceDescription,
                cancellationToken);
            return File(wavBytes, "audio/wav", "voice-design.wav");
        }
        catch (NotSupportedException)
        {
            throw new ApiException(
                ErrorCodes.BadRequest,
                $"Voice design is not supported by the selected TTS provider: {effectiveProvider}");
        }
    }

    /// <summary>
    /// Get the NanoGPT USD balance.
    /// </summary>
    [HttpGet("balance-usd")]
    public async Task<ActionResult<decimal?>> GetBalanceUsdAsync(
        [FromQuery] TtsProvider provider,
        CancellationToken cancellationToken = default)
    {
        var ttsService = serviceProvider.GetKeyedService<ITtsService>(provider);
        if (ttsService is null)
        {
            throw new ApiException(
                ErrorCodes.ExternalServiceError,
                $"TTS service '{provider}' is not registered.");
        }

        var cacheKey = $"tts-balance-usd-{provider}";
        var balance = await hybridCache.GetOrCreateAsync<ITtsService, decimal?>(
            cacheKey,
            ttsService,
            static async (service, token) => await service.GetBalanceUsdAsync(token),
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromSeconds(30),
                LocalCacheExpiration = TimeSpan.FromSeconds(30)
            },
            tags: ["tts", provider.ToString(), "balance"],
            cancellationToken: cancellationToken);

        return Ok(balance);
    }
}
