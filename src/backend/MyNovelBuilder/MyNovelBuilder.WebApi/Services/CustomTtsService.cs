using System.Text;
using System.Text.Json;
using Mapster;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Models.Tts;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Custom service for generating audio using TTS.
/// </summary>
public class CustomTtsService : ITtsService
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary></summary>
    public CustomTtsService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("http://localhost:5000");
    }
    
    /// <inheritdoc />
    public async Task<byte[]> GenerateAudioAsync(TtsRequestDto request)
    {
        var payload = request.Adapt<TtsRequest>();
        var jsonPayload = JsonSerializer.Serialize(payload, _jsonSerializerOptions);
        using var response = await _httpClient.PostAsync("generate/audio",
            new StringContent(jsonPayload, Encoding.UTF8, "application/json"));
        
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadAsByteArrayAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TtsVoiceDto>> GetVoicesAsync()
    {
        using var response = await _httpClient.GetAsync("tts/speakers");
        
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync();
        var voices = JsonSerializer.Deserialize<IEnumerable<TtsVoice>>(
            json, _jsonSerializerOptions);
        
        return voices.Adapt<IEnumerable<TtsVoiceDto>>();
    }
}
