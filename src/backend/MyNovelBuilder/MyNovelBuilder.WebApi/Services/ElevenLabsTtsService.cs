using System.Text.Json;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Exceptions;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for generating audio using ElevenLabs TTS.
/// </summary>
public class ElevenLabsTtsService : ITtsService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ElevenLabsTtsService> _logger;

    /// <inheritdoc />
    public bool SupportsEmphasisTags => true;

    /// <summary></summary>
    public ElevenLabsTtsService(IConfiguration configuration,
        HttpClient httpClient, ILogger<ElevenLabsTtsService> logger)
    {
        _httpClient = httpClient;
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
        var payload = new
        {
            text = request.Message,
            model_id = request.ModelId ?? "eleven_v3",
        };
        var jsonPayload = JsonSerializer.Serialize(payload);
        using var response = await _httpClient.PostAsync(
            $"text-to-speech/{request.VoiceId}",
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
    public Task<IEnumerable<TtsVoiceDto>> GetVoicesAsync()
    {
        // TODO: Implement fetching voices from ElevenLabs API
        return Task.FromResult(Enumerable.Empty<TtsVoiceDto>());
    }
}
