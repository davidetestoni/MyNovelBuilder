using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Hybrid;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Models.Tts;
using MyNovelBuilder.WebApi.Services;
using MyNovelBuilder.WebApi.Services.Tts;

namespace MyNovelBuilder.WebApi.Controllers;

/// <summary>
/// Controller for generating audio.
/// </summary>
[Route("api/generate/audio")]
[ApiController]
public class GenerateAudioController : ControllerBase
{
    private readonly ILogger<GenerateAudioController> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IIntegrationsService _integrationsService;
    private readonly HybridCache _hybridCache;
    private readonly ITtsAudioGenerationService _ttsAudioGenerationService;
    private readonly IImmersiveTtsService _immersiveTtsService;

    /// <summary></summary>
    public GenerateAudioController(
        ILogger<GenerateAudioController> logger,
        IServiceProvider serviceProvider,
        IIntegrationsService integrationsService,
        HybridCache hybridCache,
        ITtsAudioGenerationService ttsAudioGenerationService,
        IImmersiveTtsService immersiveTtsService)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _integrationsService = integrationsService;
        _hybridCache = hybridCache;
        _ttsAudioGenerationService = ttsAudioGenerationService;
        _immersiveTtsService = immersiveTtsService;
    }
    
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
            var config = await _integrationsService.GetConfigAsync(cancellationToken);
            ttsProvider = config.TtsProvider;
        }
        
        var ttsService = _serviceProvider.GetKeyedService<ITtsService>(ttsProvider);

        if (ttsService is null)
        {
            _logger.LogError(
                "Unsupported TTS provider: {Provider}", ttsProvider);

            throw new InvalidOperationException(
                $"Unsupported TTS provider: {ttsProvider}");
        }

        return ttsService;
    }
    /// <summary>
    /// Generate audio from text.
    /// </summary>
    [HttpPost("tts")]
    public async Task<ActionResult> GenerateAudioAsync(
        TtsRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating audio for textLength={TextLength}.", dto.Message.Length);
        _logger.LogDebug("Generating audio for text: {Text}", dto.Message);
        var wavBytes = await _ttsAudioGenerationService.GenerateWavBytesAsync(
            new TextToSpeechGenerationRequest
            {
                Message = dto.Message,
                Provider = dto.Provider,
                TtsModelId = dto.ModelId,
                VoiceId = dto.VoiceId
            },
            cancellationToken);

        return File(wavBytes, "audio/wav", "audio.wav");
    }

    /// <summary>
    /// Generate audio stream from text.
    /// </summary>
    [HttpPost("tts/stream")]
    public async Task<ActionResult> GenerateAudioStreamAsync(
        TtsRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating audio stream for textLength={TextLength}.", dto.Message.Length);
        _logger.LogDebug("Generating audio stream for text: {Text}", dto.Message);
        var audioStream = await _ttsAudioGenerationService.GenerateWavStreamAsync(
            new TextToSpeechGenerationRequest
            {
                Message = dto.Message,
                Provider = dto.Provider,
                TtsModelId = dto.ModelId,
                VoiceId = dto.VoiceId
            },
            cancellationToken);

        return File(audioStream, "audio/wav", "audio.wav");
    }

    /// <summary>
    /// Prepare immersive TTS chunks without generating audio.
    /// </summary>
    [HttpPost("tts/immersive/debug")]
    public async Task<ActionResult<ImmersiveTtsDebugResponseDto>> DebugImmersiveTtsAsync(
        ImmersiveTtsRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _immersiveTtsService.PrepareDebugAsync(dto, cancellationToken));
    }

    /// <summary>
    /// Generate immersive multi-speaker audio stream for a prose section.
    /// </summary>
    [HttpPost("tts/immersive/stream")]
    public async Task<ActionResult> GenerateImmersiveAudioStreamAsync(
        ImmersiveTtsRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        var audioStream = await _immersiveTtsService.GenerateStreamAsync(dto, cancellationToken);
        return File(audioStream, "audio/wav", "audio.wav");
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
        var modelsWithFeatures = models.Select(model =>
        {
            model.SupportsTextEmphasis = ttsService.SupportsTextEmphasis(model.ModelId);
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
                    _serviceProvider.GetKeyedService<ITtsService>(provider)?.SupportsVoiceDesign() ?? false
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
        var ttsService = _serviceProvider.GetKeyedService<ITtsService>(provider);
        if (ttsService is null)
        {
            throw new ApiException(
                ErrorCodes.ExternalServiceError,
                $"TTS service '{provider}' is not registered.");
        }

        var cacheKey = $"tts-balance-usd-{provider}";
        var balance = await _hybridCache.GetOrCreateAsync<ITtsService, decimal?>(
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
