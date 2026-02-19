using System.Text;
using System.Text.Json;
using Mapster;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Models.Tts;

using MyNovelBuilder.WebApi.Attributes;

namespace MyNovelBuilder.WebApi.Services.Tts;

/// <summary>
/// Custom service for generating audio using TTS.
/// </summary>
[RegisterKeyedService(TtsProvider.Custom, useHttpClient: true)]
public class CustomTtsService : ITtsService
{
    private readonly HttpClient _httpClient;
    private readonly IIntegrationsService _integrationsService;

    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <inheritdoc />
    public bool SupportsEmphasisTags => false;
    
    /// <inheritdoc />
    public AudioFormat OutputAudioFormat => AudioFormat.Mp3;
    
    /// <summary></summary>
    public CustomTtsService(
        HttpClient httpClient,
        IIntegrationsService integrationsService)
    {
        _httpClient = httpClient;
        _integrationsService = integrationsService;
        _httpClient.BaseAddress = new Uri("http://localhost:5000");
    }

    /// <inheritdoc />
    public async Task<byte[]> GenerateAudioAsync(
        TtsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var config = await _integrationsService.GetConfigAsync(cancellationToken);
        
        var jsonPayload = JsonSerializer.Serialize(new TtsRequest
        {
            Message = request.Message,
            VoiceId = config.TtsVoiceId
        }, _jsonSerializerOptions);
        using var response = await _httpClient.PostAsync(
            "generate/audio",
            new StringContent(jsonPayload, Encoding.UTF8, "application/json"),
            cancellationToken);
        
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<Stream> GenerateAudioStreamAsync(
        TtsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TtsVoiceDto>> GetVoicesAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync("tts/speakers", cancellationToken);
        
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var voices = JsonSerializer.Deserialize<IEnumerable<TtsVoice>>(
            json, _jsonSerializerOptions);
        
        return voices.Adapt<IEnumerable<TtsVoiceDto>>();
    }
}
