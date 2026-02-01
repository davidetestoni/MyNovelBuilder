using System.Text.Json;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;

namespace MyNovelBuilder.WebApi.Services.Tts;

/// <summary>
/// Service for generating audio using ElevenLabs TTS.
/// </summary>
public class ElevenLabsTtsService : ITtsService
{
    private readonly HttpClient _httpClient;
    private readonly IIntegrationsService _integrationsService;
    private readonly ILogger<ElevenLabsTtsService> _logger;

    /// <inheritdoc />
    public bool SupportsEmphasisTags => true;
    
    /// <inheritdoc />
    public AudioFormat OutputAudioFormat => AudioFormat.Mp3;

    /// <summary></summary>
    public ElevenLabsTtsService(
        ILogger<ElevenLabsTtsService> logger,
        IConfiguration configuration,
        HttpClient httpClient,
        IIntegrationsService integrationsService)
    {
        _httpClient = httpClient;
        _integrationsService = integrationsService;
        _logger = logger;
        _httpClient.BaseAddress = new Uri("https://api.elevenlabs.io/v1/");
        
        var apiKey = configuration["Secrets:ElevenLabsApiKey"];
        
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ApiException(
                ErrorCodes.MissingOrInvalidServiceCredentials,
                "ElevenLabs API key is missing.");
        }
        
        _httpClient.DefaultRequestHeaders.Add("xi-api-key", apiKey);
    }

    /// <inheritdoc />
    public async Task<byte[]> GenerateAudioAsync(TtsRequestDto request)
    {
        var config = await _integrationsService.GetConfigAsync();
        
        var payload = new
        {
            text = request.Message,
            model_id = "eleven_v3",
        };
        var jsonPayload = JsonSerializer.Serialize(payload);
        using var response = await _httpClient.PostAsync(
            $"text-to-speech/{config.TtsVoiceId}",
            new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json"));

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("ElevenLabs TTS generation failed: {ErrorContent}", errorContent);
            throw new ApiException(ErrorCodes.ExternalServiceError,
                $"ElevenLabs refused to generate audio: {errorContent}");
        }
        
        return await response.Content.ReadAsByteArrayAsync();
    }
    
    /// <inheritdoc />
    public Task<Stream> GenerateAudioStreamAsync(TtsRequestDto request)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task<IEnumerable<TtsVoiceDto>> GetVoicesAsync()
    {
        // TODO: Implement fetching voices from ElevenLabs API
        return Task.FromResult(Enumerable.Empty<TtsVoiceDto>());
    }
}
