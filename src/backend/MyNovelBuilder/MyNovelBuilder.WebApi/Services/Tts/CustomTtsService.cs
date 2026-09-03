using System.Text;
using System.Text.Json;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Helpers;
using MyNovelBuilder.WebApi.Models.Integrations;
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
    public AudioFormat OutputAudioFormat => AudioFormat.Mp3;
    
    /// <summary></summary>
    public CustomTtsService(
        HttpClient httpClient,
        IIntegrationsService integrationsService)
    {
        _httpClient = httpClient;
        _integrationsService = integrationsService;
    }

    /// <inheritdoc />
    public async Task<byte[]> GenerateAudioAsync(
        TtsRequest request,
        CancellationToken cancellationToken = default)
    {
        var jsonPayload = JsonSerializer.Serialize(new TtsRequest
        {
            ModelId = request.ModelId,
            Message = request.Message,
            VoiceId = request.VoiceId
        }, _jsonSerializerOptions);
        var requestUri = await CreateRequestUriAsync("generate/audio", cancellationToken);
        using var response = await _httpClient.PostAsync(
            requestUri,
            new StringContent(jsonPayload, Encoding.UTF8, "application/json"),
            cancellationToken);
        
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<Stream> GenerateAudioStreamAsync(
        TtsRequest request,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TtsModelDto>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(
            await CreateRequestUriAsync("tts/speakers", cancellationToken),
            cancellationToken);
        
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var voices = JsonSerializer.Deserialize<IEnumerable<TtsVoice>>(
            json, _jsonSerializerOptions) ?? [];

        return
        [
            new TtsModelDto
            {
                ModelId = "default",
                Name = "Default Model",
                Voices = voices.Select(v => new TtsVoiceDto
                {
                    VoiceId = v.VoiceId,
                    Name = v.Name,
                    PreviewUrl = v.PreviewUrl,
                    Language = WritingLanguage.English
                })
            }
        ];
    }

    /// <inheritdoc />
    public Task<decimal?> GetBalanceUsdAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<decimal?>(null);

    private async Task<Uri> CreateRequestUriAsync(string relativePath, CancellationToken cancellationToken)
    {
        var config = await _integrationsService.GetConfigAsync(cancellationToken);
        var baseUri = ProviderBaseUrlHelper.NormalizeHttpBaseUri(
            config.CustomTtsBaseUrl,
            IntegrationsConfig.DefaultCustomTtsBaseUrl,
            "Custom TTS");

        return ProviderBaseUrlHelper.CreateRequestUri(baseUri, relativePath);
    }
}
