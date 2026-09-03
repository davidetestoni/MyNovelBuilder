using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyNovelBuilder.WebApi.Attributes;
using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Helpers;
using MyNovelBuilder.WebApi.Models.Integrations;
using MyNovelBuilder.WebApi.Models.Tts;
using MyNovelBuilder.WebApi.Options;
using NAudio.Wave;

namespace MyNovelBuilder.WebApi.Services.Tts;

/// <summary>
/// Text-to-speech service for Qwen3.
/// </summary>
[RegisterKeyedService(TtsProvider.Qwen3, useHttpClient: true)]
public class Qwen3TtsService : ITtsService
{
    private readonly HttpClient _httpClient;
    private readonly IIntegrationsService _integrationsService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly string _voicesFolder;
    private const int _maxChunkLength = 500;
    private const int _sampleRate = 24000;
    private const string _defaultLanguageCode = "en";

    /// <inheritdoc />
    public AudioFormat OutputAudioFormat => AudioFormat.Wav;

    /// <inheritdoc />
    public bool SupportsVoiceDesign() => true;

    /// <summary></summary>
    public Qwen3TtsService(
        HttpClient httpClient,
        IOptions<AppStorageOptions> storageOptions,
        IServiceScopeFactory serviceScopeFactory,
        IIntegrationsService integrationsService)
    {
        _httpClient = httpClient;
        _integrationsService = integrationsService;
        _serviceScopeFactory = serviceScopeFactory;
        _voicesFolder = Path.Combine(storageOptions.Value.DataFolder, "voices");
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
    }

    /// <inheritdoc />
    public async Task<byte[]> VoiceDesignAsync(
        string prompt,
        WritingLanguage language,
        string voiceDescription,
        CancellationToken cancellationToken = default)
    {
        using var formData = new MultipartFormDataContent();
        formData.Add(new StringContent(NormalizeText(prompt)), "prompt");
        formData.Add(new StringContent(MapToQwen3LanguageCode(language)), "language");
        formData.Add(new StringContent(voiceDescription.Trim()), "voice_description");

        using var response = await _httpClient.PostAsync(
            await CreateRequestUriAsync("voice-design", cancellationToken),
            formData,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private static string NormalizeText(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        return input
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
            .Replace('\u2013', ',')
            .Replace('\u2014', ',')
            .Replace('\u2015', ',')
            .Replace("\u2026", "...")
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

        var languageCode = await ResolveLanguageCodeAsync(request.VoiceId, cancellationToken);

        await using var fullPcmStream = new MemoryStream();

        foreach (var chunk in textChunks)
        {
            using var formData = new MultipartFormDataContent();
            formData.Add(new StringContent(NormalizeText(chunk)), "text");
            formData.Add(new StringContent(languageCode), "language");

            if (referenceWavPath is not null)
            {
                await using var referenceWavStream = File.OpenRead(referenceWavPath);
                using var wavContent = new StreamContent(referenceWavStream);
                wavContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
                formData.Add(
                    wavContent,
                    "reference_wav",
                    Path.GetFileName(referenceWavPath));

                var requestUri = await CreateRequestUriAsync("tts", cancellationToken);
                using var response = await _httpClient.PostAsync(
                    requestUri,
                    formData,
                    cancellationToken);
                response.EnsureSuccessStatusCode();

                var pcmChunk = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                await fullPcmStream.WriteAsync(pcmChunk, cancellationToken);
            }
            else
            {
                var requestUri = await CreateRequestUriAsync("tts", cancellationToken);
                using var response = await _httpClient.PostAsync(
                    requestUri,
                    formData,
                    cancellationToken);
                response.EnsureSuccessStatusCode();

                var pcmChunk = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                await fullPcmStream.WriteAsync(pcmChunk, cancellationToken);
            }
        }

        var combinedPcm = fullPcmStream.ToArray();
        using var wavStream = new MemoryStream();
        await using (var writer = new WaveFileWriter(wavStream, new WaveFormat(_sampleRate, 16, 1)))
        {
            await writer.WriteAsync(combinedPcm, cancellationToken);
        }

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
                var languageCode = await ResolveLanguageCodeAsync(request.VoiceId, ct);

                foreach (var chunk in textChunks)
                {
                    using var formData = new MultipartFormDataContent();
                    formData.Add(new StringContent(NormalizeText(chunk)), "text");
                    formData.Add(new StringContent(languageCode), "language");

                    if (referenceWavPath is not null)
                    {
                        await using var referenceWavStream = File.OpenRead(referenceWavPath);
                        using var wavContent = new StreamContent(referenceWavStream);
                        wavContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
                        formData.Add(
                            wavContent,
                            "reference_wav",
                            Path.GetFileName(referenceWavPath));

                        var requestUri = await CreateRequestUriAsync("tts", ct);
                        using var response = await _httpClient.PostAsync(
                            requestUri,
                            formData,
                            ct);
                        response.EnsureSuccessStatusCode();

                        var pcmChunk = await response.Content.ReadAsByteArrayAsync(ct);
                        await writeAsync(pcmChunk);
                    }
                    else
                    {
                        var requestUri = await CreateRequestUriAsync("tts", ct);
                        using var response = await _httpClient.PostAsync(
                            requestUri,
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
    public async Task<IEnumerable<TtsModelDto>> GetModelsAsync(CancellationToken cancellationToken = default)
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
                Language = v.Language
            })
            .ToListAsync(cancellationToken);

        return
        [
            new TtsModelDto
            {
                ModelId = "Qwen3-TTS-12Hz-1.7B-Base",
                Name = "Qwen3-TTS 12Hz 1.7B",
                Voices = customVoices
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
            config.Qwen3BaseUrl,
            IntegrationsConfig.DefaultQwen3BaseUrl,
            "Qwen3");

        return ProviderBaseUrlHelper.CreateRequestUri(baseUri, relativePath);
    }

    private string? GetReferenceWavPath(string? voiceId)
    {
        if (!Guid.TryParse(voiceId, out var id))
        {
            throw new ApiException(
                ErrorCodes.BadRequest,
                $"Invalid Qwen3 voice ID: {voiceId}");
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

    private async Task<string> ResolveLanguageCodeAsync(string? voiceId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(voiceId, out var id))
        {
            return _defaultLanguageCode;
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var voiceLanguage = await dbContext.Voices
            .AsNoTracking()
            .Where(v => v.Id == id)
            .Select(v => (WritingLanguage?)v.Language)
            .FirstOrDefaultAsync(cancellationToken);

        return voiceLanguage.HasValue
            ? MapToQwen3LanguageCode(voiceLanguage.Value)
            : _defaultLanguageCode;
    }

    private static string MapToQwen3LanguageCode(WritingLanguage language)
    {
        return language switch
        {
            WritingLanguage.English => "english",
            WritingLanguage.Italian => "italian",
            WritingLanguage.French => "french",
            WritingLanguage.Spanish => "spanish",
            WritingLanguage.German => "german",
            WritingLanguage.Russian => "russian",
            _ => _defaultLanguageCode
        };
    }
}
