using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Text-to-speech service implementation for Pocket TTS.
/// </summary>
public class PocketTtsService : ITtsService
{
    private readonly HttpClient _httpClient;

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
    public PocketTtsService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
    
    private static MultipartFormDataContent CreateRequestContent(TtsRequestDto request)
    {
        var content = new MultipartFormDataContent();
        
        // Multipart content with "text" and "voice_url" fields
        content.Add(new StringContent(request.Message), "text");
        content.Add(new StringContent(request.VoiceId), "voice_url");

        return content;
    }
    
    /// <inheritdoc/>
    public async Task<byte[]> GenerateAudioAsync(TtsRequestDto request)
    {
        using var response = await _httpClient.PostAsync(
            "http://localhost:8000/tts", CreateRequestContent(request));
        
        // The response is a wav file, so just return it
        return await response.Content.ReadAsByteArrayAsync();
    }
    
    /// <inheritdoc />
    public async Task<Stream> GenerateAudioStreamAsync(TtsRequestDto request)
    {
        using var httpRequest = new HttpRequestMessage();
        httpRequest.RequestUri = new Uri("http://localhost:8000/tts");
        httpRequest.Method = HttpMethod.Post;
        httpRequest.Content = CreateRequestContent(request);
        
        var response = await _httpClient.SendAsync(
            httpRequest, HttpCompletionOption.ResponseHeadersRead);
        
        return await response.Content.ReadAsStreamAsync();
    }
    
    /// <inheritdoc/>
    public Task<IEnumerable<TtsVoiceDto>> GetVoicesAsync() =>
        Task.FromResult<IEnumerable<TtsVoiceDto>>(_voices);
}
