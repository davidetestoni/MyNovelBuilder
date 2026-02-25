using System.Text;
using System.Text.Json;
using MyNovelBuilder.WebApi.Attributes;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Helpers;
using NAudio.Wave;

namespace MyNovelBuilder.WebApi.Services.Tts;

/// <summary>
/// Text-to-speech service for Chatterbox.
/// </summary>
[RegisterKeyedService(TtsProvider.Chatterbox, useHttpClient: true)]
public class ChatterboxTtsService : ITtsService
{
    private readonly HttpClient _httpClient;
    private readonly IIntegrationsService _integrationsService;
    private const int _maxChunkLength = 500;
    private const int _sampleRate = 24000;

    /// <inheritdoc />
    public bool SupportsEmphasisTags => false;

    /// <inheritdoc />
    public AudioFormat OutputAudioFormat => AudioFormat.Wav;

    /// <summary></summary>
    public ChatterboxTtsService(
        HttpClient httpClient,
        IIntegrationsService integrationsService)
    {
        _httpClient = httpClient;
        _integrationsService = integrationsService;
        _httpClient.BaseAddress = new Uri("http://localhost:8000");
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
    }

    private static string NormalizeText(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        return input
            // Quotes/apostrophes
            .Replace('\u2018', '\'')
            .Replace('\u2019', '\'')
            .Replace('\u201B', '\'')
            .Replace('\u2032', '\'')
            .Replace('\u2035', '\'')
            .Replace('\u201C', '"')
            .Replace('\u201D', '"')
            .Replace('\u201F', '"')
            .Replace('\u2033', '"')
            .Replace('\u2036', '"')
            // Dashes and ellipsis
            .Replace('\u2013', ',')
            .Replace('\u2014', ',')
            .Replace('\u2015', ',')
            .Replace("\u2026", "...")
            // Spacing / separators
            .Replace('\u00A0', ' ')
            .Replace('\u202F', ' ')
            .Replace('\u2007', ' ')
            .Replace('\u2028', '\n')
            .Replace('\u2029', '\n');
    }
    
    /// <inheritdoc />
    public async Task<byte[]> GenerateAudioAsync(
        TtsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var config = await _integrationsService.GetConfigAsync(cancellationToken);
        var textChunks = new TextChunker(_maxChunkLength).ChunkText(request.Message);

        if (textChunks.Count == 0)
        {
            return [];
        }

        await using var fullPcmStream = new MemoryStream();

        foreach (var chunk in textChunks)
        {
            var jsonPayload = JsonSerializer.Serialize(new
            {
                text = NormalizeText(chunk),
                voice = config.TtsVoiceId
            });

            using var response = await _httpClient.PostAsync(
                "tts",
                new StringContent(jsonPayload, Encoding.UTF8, "application/json"),
                cancellationToken);
            response.EnsureSuccessStatusCode();

            var pcmChunk = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            await fullPcmStream.WriteAsync(pcmChunk, cancellationToken);
        }

        var combinedPcm = fullPcmStream.ToArray();
        using var wavStream = new MemoryStream();
        await using var writer = new WaveFileWriter(wavStream, new WaveFormat(_sampleRate, 16, 1));
        await writer.WriteAsync(combinedPcm, cancellationToken);

        return wavStream.ToArray();
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
        using var response = await _httpClient.GetAsync("voices", cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var voices = JsonSerializer.Deserialize<List<string>>(json) ?? [];

        return voices.Select(v => new TtsVoiceDto
        {
            VoiceId = v,
            Name = v,
        });
    }
}
