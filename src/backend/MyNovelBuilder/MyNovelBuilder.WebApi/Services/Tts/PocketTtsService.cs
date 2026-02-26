using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Models.Tts;

using MyNovelBuilder.WebApi.Attributes;

namespace MyNovelBuilder.WebApi.Services.Tts;

/// <summary>
/// Text-to-speech service implementation for Pocket TTS.
/// </summary>
[RegisterKeyedService(TtsProvider.PocketTts, useHttpClient: true)]
public class PocketTtsService : ITtsService
{
    private readonly HttpClient _httpClient;

    private readonly TtsVoiceDto[] _voices = new List<string>(
            ["alba", "marius", "javert", "jean", "fantine", "cosette", "eponine", "azelma"])
        .Select(v => new TtsVoiceDto
        {
            VoiceId = v,
            Name = char.ToUpper(v[0]) + v[1..],
            Language = WritingLanguage.English
        }).ToArray();

    /// <inheritdoc/>
    public bool SupportsEmphasisTags => false;
    
    /// <inheritdoc />
    public AudioFormat OutputAudioFormat => AudioFormat.Wav;
    
    /// <summary></summary>
    public PocketTtsService(
        HttpClient httpClient)
    {
        _httpClient = httpClient;
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
    public async Task<byte[]> GenerateAudioAsync(
        TtsRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync(
            "http://localhost:8000/tts", CreateRequestContent(
                request.Message, request.VoiceId),
            cancellationToken);
        
        // The response is a wav file, so just return it
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }
    
    /// <inheritdoc />
    public async Task<Stream> GenerateAudioStreamAsync(
        TtsRequest request,
        CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage();
        httpRequest.RequestUri = new Uri("http://localhost:8000/tts");
        httpRequest.Method = HttpMethod.Post;
        httpRequest.Content = CreateRequestContent(
            request.Message, request.VoiceId);
        
        var response = await _httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        
        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }
    
    /// <inheritdoc/>
    public Task<IEnumerable<TtsVoiceDto>> GetVoicesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<TtsVoiceDto>>(_voices);
}
