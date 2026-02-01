using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Services.Tts;

/// <summary>
/// Text-to-speech service implementation for Pocket TTS.
/// </summary>
public class PocketTtsService : ITtsService
{
    private readonly HttpClient _httpClient;
    private readonly IIntegrationsService _integrationsService;

    private readonly TtsVoiceDto[] _voices = new List<string>(
            ["alba", "marius", "javert", "jean", "fantine", "cosette", "eponine", "azelma"])
        .Select(v => new TtsVoiceDto
        {
            VoiceId = v,
            Name = char.ToUpper(v[0]) + v[1..],
        }).ToArray();

    /// <inheritdoc/>
    public bool SupportsEmphasisTags => false;
    
    /// <inheritdoc />
    public AudioFormat OutputAudioFormat => AudioFormat.Wav;
    
    /// <summary></summary>
    public PocketTtsService(
        HttpClient httpClient,
        IIntegrationsService integrationsService)
    {
        _httpClient = httpClient;
        _integrationsService = integrationsService;
    }
    
    private static MultipartFormDataContent CreateRequestContent(
        string message, string voiceId)
    {
        var content = new MultipartFormDataContent();
        
        // Multipart content with "text" and "voice_url" fields
        content.Add(new StringContent(message), "text");
        content.Add(new StringContent(voiceId), "voice_url");

        return content;
    }
    
    /// <inheritdoc/>
    public async Task<byte[]> GenerateAudioAsync(TtsRequestDto request)
    {
        var config = await _integrationsService.GetConfigAsync();
        
        using var response = await _httpClient.PostAsync(
            "http://localhost:8000/tts", CreateRequestContent(
                request.Message, config.TtsVoiceId));
        
        // The response is a wav file, so just return it
        return await response.Content.ReadAsByteArrayAsync();
    }
    
    /// <inheritdoc />
    public async Task<Stream> GenerateAudioStreamAsync(TtsRequestDto request)
    {
        var config = await _integrationsService.GetConfigAsync();
        
        using var httpRequest = new HttpRequestMessage();
        httpRequest.RequestUri = new Uri("http://localhost:8000/tts");
        httpRequest.Method = HttpMethod.Post;
        httpRequest.Content = CreateRequestContent(
            request.Message, config.TtsVoiceId);
        
        var response = await _httpClient.SendAsync(
            httpRequest, HttpCompletionOption.ResponseHeadersRead);
        
        return await response.Content.ReadAsStreamAsync();
    }
    
    /// <inheritdoc/>
    public Task<IEnumerable<TtsVoiceDto>> GetVoicesAsync() =>
        Task.FromResult<IEnumerable<TtsVoiceDto>>(_voices);
}
