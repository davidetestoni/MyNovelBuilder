using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyNovelBuilder.WebApi.Attributes;
using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Helpers;
using MyNovelBuilder.WebApi.Models.Tts;
using MyNovelBuilder.WebApi.Options;
using NAudio.Wave;

namespace MyNovelBuilder.WebApi.Services.Tts;

/// <summary>
/// Text-to-speech service for Chatterbox.
/// </summary>
[RegisterKeyedService(TtsProvider.Chatterbox, useHttpClient: true)]
public class ChatterboxTtsService : ITtsService
{
    private readonly HttpClient _httpClient;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly string _voicesFolder;
    private const int _maxChunkLength = 500;
    private const int _sampleRate = 24000;
    private const string _defaultVoiceId = "default";

    /// <inheritdoc />
    public bool SupportsEmphasisTags => false;

    /// <inheritdoc />
    public AudioFormat OutputAudioFormat => AudioFormat.Wav;

    /// <summary></summary>
    public ChatterboxTtsService(
        HttpClient httpClient,
        IOptions<AppStorageOptions> storageOptions,
        IServiceScopeFactory serviceScopeFactory)
    {
        _httpClient = httpClient;
        _serviceScopeFactory = serviceScopeFactory;
        _voicesFolder = Path.Combine(storageOptions.Value.DataFolder, "voices");
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
        TtsRequest request,
        CancellationToken cancellationToken = default)
    {
        var textChunks = new TextChunker(_maxChunkLength).ChunkText(request.Message);
        var referenceWavPath = GetReferenceWavPath(request.VoiceId);

        if (textChunks.Count == 0)
        {
            return [];
        }

        await using var fullPcmStream = new MemoryStream();

        foreach (var chunk in textChunks)
        {
            using var formData = new MultipartFormDataContent();
            formData.Add(new StringContent(NormalizeText(chunk)), "text");

            if (referenceWavPath is not null)
            {
                await using var referenceWavStream = File.OpenRead(referenceWavPath);
                using var wavContent = new StreamContent(referenceWavStream);
                wavContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
                formData.Add(
                    wavContent,
                    "reference_wav",
                    Path.GetFileName(referenceWavPath));

                using var response = await _httpClient.PostAsync(
                    "tts",
                    formData,
                    cancellationToken);
                response.EnsureSuccessStatusCode();

                var pcmChunk = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                await fullPcmStream.WriteAsync(pcmChunk, cancellationToken);
            }
            else
            {
                using var response = await _httpClient.PostAsync(
                    "tts",
                    formData,
                    cancellationToken);
                response.EnsureSuccessStatusCode();

                var pcmChunk = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                await fullPcmStream.WriteAsync(pcmChunk, cancellationToken);
            }
        }

        var combinedPcm = fullPcmStream.ToArray();
        using var wavStream = new MemoryStream();
        await using var writer = new WaveFileWriter(wavStream, new WaveFormat(_sampleRate, 16, 1));
        await writer.WriteAsync(combinedPcm, cancellationToken);

        return wavStream.ToArray();
    }

    /// <inheritdoc />
    public Task<Stream> GenerateAudioStreamAsync(
        TtsRequest request,
        CancellationToken cancellationToken = default)
    {
        var textChunks = new TextChunker(_maxChunkLength).ChunkText(request.Message);
        var referenceWavPath = GetReferenceWavPath(request.VoiceId);

        return Task.FromResult<Stream>(new PcmWavStreamingStream(
            sampleRate: _sampleRate,
            channels: 1,
            bitsPerSample: 16,
            producer: async (writeAsync, ct) =>
            {
                foreach (var chunk in textChunks)
                {
                    using var formData = new MultipartFormDataContent();
                    formData.Add(new StringContent(NormalizeText(chunk)), "text");

                    if (referenceWavPath is not null)
                    {
                        await using var referenceWavStream = File.OpenRead(referenceWavPath);
                        using var wavContent = new StreamContent(referenceWavStream);
                        wavContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
                        formData.Add(
                            wavContent,
                            "reference_wav",
                            Path.GetFileName(referenceWavPath));

                        using var response = await _httpClient.PostAsync(
                            "tts",
                            formData,
                            ct);
                        response.EnsureSuccessStatusCode();

                        var pcmChunk = await response.Content.ReadAsByteArrayAsync(ct);
                        await writeAsync(pcmChunk);
                    }
                    else
                    {
                        using var response = await _httpClient.PostAsync(
                            "tts",
                            formData,
                            ct);
                        response.EnsureSuccessStatusCode();

                        var pcmChunk = await response.Content.ReadAsByteArrayAsync(ct);
                        await writeAsync(pcmChunk);
                    }
                }
            },
            ct: cancellationToken));
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TtsVoiceDto>> GetVoicesAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var customVoices = await dbContext.Voices
            .AsNoTracking()
            .OrderBy(v => v.Name)
            .Select(v => new TtsVoiceDto
            {
                VoiceId = v.Id.ToString(),
                Name = v.Name,
            })
            .ToListAsync(cancellationToken);

        var defaultVoice = new TtsVoiceDto
        {
            VoiceId = _defaultVoiceId,
            Name = "Default"
        };

        return [defaultVoice, .. customVoices];
    }

    private string? GetReferenceWavPath(string? voiceId)
    {
        if (string.IsNullOrWhiteSpace(voiceId)
            || voiceId.Equals(_defaultVoiceId, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!Guid.TryParse(voiceId, out var id))
        {
            throw new ApiException(
                ErrorCodes.BadRequest,
                $"Invalid Chatterbox voice ID: {voiceId}");
        }

        var wavPath = Path.Combine(_voicesFolder, $"{id}.wav");

        if (!File.Exists(wavPath))
        {
            throw new ApiException(
                ErrorCodes.InvalidFile,
                $"Voice sample file was not found for voice ID: {voiceId}");
        }

        return wavPath;
    }
}
