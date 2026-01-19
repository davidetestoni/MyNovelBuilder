using MyNovelBuilder.WebApi.Dtos.Generate;

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
    
    /// <inheritdoc/>
    public Task<IEnumerable<TtsVoiceDto>> GetVoicesAsync() =>
        Task.FromResult<IEnumerable<TtsVoiceDto>>(_voices);
}
