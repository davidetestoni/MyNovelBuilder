using System.Text.Json;
using System.Text.Json.Nodes;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;

using MyNovelBuilder.WebApi.Attributes;

namespace MyNovelBuilder.WebApi.Services.Tts;

/// <summary>
/// Service for generating audio using ElevenLabs TTS.
/// </summary>
[RegisterKeyedService(TtsProvider.ElevenLabs, useHttpClient: true)]
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
        HttpClient httpClient,
        IIntegrationsService integrationsService)
    {
        _httpClient = httpClient;
        _integrationsService = integrationsService;
        _logger = logger;
        _httpClient.BaseAddress = new Uri("https://api.elevenlabs.io/v2/");
    }

    /// <inheritdoc />
    public async Task<byte[]> GenerateAudioAsync(TtsRequestDto request)
    {
        var config = await _integrationsService.GetConfigAsync();
        var apiKey = config.ElevenLabsApiKey;
        
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ApiException(
                ErrorCodes.MissingOrInvalidServiceCredentials,
                "ElevenLabs API key is missing.");
        }
        
        var httpRequest = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri(
                _httpClient.BaseAddress!,
                $"text-to-speech/{config.TtsVoiceId}"),
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    text = request.Message,
                    model_id = "eleven_v3",
                }),
                System.Text.Encoding.UTF8,
                "application/json")
        };
        httpRequest.Headers.TryAddWithoutValidation("xi-api-key", apiKey);
        
        using var response = await _httpClient.SendAsync(httpRequest);

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
    public async Task<IEnumerable<TtsVoiceDto>> GetVoicesAsync()
    {
        var config = await _integrationsService.GetConfigAsync();
        var apiKey = config.ElevenLabsApiKey;
        
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ApiException(
                ErrorCodes.MissingOrInvalidServiceCredentials,
                "ElevenLabs API key is missing.");
        }
        
        var httpRequest = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(_httpClient.BaseAddress!, "voices"),
        };
        httpRequest.Headers.TryAddWithoutValidation("xi-api-key", apiKey);
        
        // Response is in the format:
        // { "voices": [ { "voice_id": "...", "name": "..." }, ... ] }
        using var response = await _httpClient.SendAsync(httpRequest);
        var jsonResponse = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("ElevenLabs GetVoices failed: {Response}", jsonResponse);
            throw new ApiException(ErrorCodes.ExternalServiceError,
                $"ElevenLabs refused to provide voices: {jsonResponse}");
        }
        
        var responseObject = JsonNode.Parse(jsonResponse);
        var voicesArray = responseObject!["voices"]!.AsArray();
        
        return voicesArray.Select(v => new TtsVoiceDto
        {
            VoiceId = v!["voice_id"]!.GetValue<string>(),
            Name = v["name"]!.GetValue<string>(),
        });
    }
}
