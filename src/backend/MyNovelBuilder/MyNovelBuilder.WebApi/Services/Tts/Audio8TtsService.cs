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
/// Text-to-speech service for Audio8.
/// </summary>
[RegisterKeyedService(TtsProvider.Audio8, useHttpClient: true)]
public class Audio8TtsService : ITtsService
{
    private readonly HttpClient _httpClient;
    private readonly IIntegrationsService _integrationsService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly string _voicesFolder;
    private const int _maxChunkLength = 500;
    private const int _sampleRate = 44100;
    private const string _defaultVoiceId = "default";

    /// <inheritdoc />
    public AudioFormat OutputAudioFormat => AudioFormat.Wav;

    /// <summary></summary>
    public Audio8TtsService(
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
    public async Task<byte[]> GenerateAudioAsync(
        TtsRequest request,
        CancellationToken cancellationToken = default)
    {
        var textChunks = new TextChunker(_maxChunkLength).ChunkText(request.Message);

        if (textChunks.Count == 0)
        {
            return [];
        }

        var voiceReference = await ResolveVoiceReferenceAsync(request.VoiceId, cancellationToken);
        var responseSampleRate = _sampleRate;
        await using var fullPcmStream = new MemoryStream();

        foreach (var chunk in textChunks)
        {
            var result = await GeneratePcmChunkAsync(chunk, voiceReference, cancellationToken);
            responseSampleRate = result.SampleRate;
            await fullPcmStream.WriteAsync(result.Pcm, cancellationToken);
        }

        using var wavStream = new MemoryStream();
        await using (var writer = new WaveFileWriter(
                         wavStream,
                         new WaveFormat(responseSampleRate, 16, 1)))
        {
            await writer.WriteAsync(fullPcmStream.ToArray(), cancellationToken);
        }

        return wavStream.ToArray();
    }

    /// <inheritdoc />
    public Task<Stream> GenerateAudioStreamAsync(
        TtsRequest request,
        CancellationToken cancellationToken = default)
    {
        var textChunks = new TextChunker(_maxChunkLength).ChunkText(request.Message);

        return Task.FromResult<Stream>(new PcmWavStreamingStream(
            sampleRate: _sampleRate,
            channels: 1,
            bitsPerSample: 16,
            producer: async (writeAsync, ct) =>
            {
                var voiceReference = await ResolveVoiceReferenceAsync(request.VoiceId, ct);

                foreach (var chunk in textChunks)
                {
                    var result = await GeneratePcmChunkAsync(chunk, voiceReference, ct);
                    await writeAsync(result.Pcm);
                }
            },
            ct: cancellationToken));
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TtsModelDto>> GetModelsAsync(
        CancellationToken cancellationToken = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var customVoices = await dbContext.Voices
            .AsNoTracking()
            .Where(voice => voice.Transcript != null && voice.Transcript.Trim() != "")
            .OrderBy(voice => voice.Name)
            .Select(voice => new TtsVoiceDto
            {
                VoiceId = voice.Id.ToString(),
                Name = voice.Name,
                Language = voice.Language
            })
            .ToListAsync(cancellationToken);

        var defaultVoice = new TtsVoiceDto
        {
            VoiceId = _defaultVoiceId,
            Name = "Default",
            Language = WritingLanguage.English
        };

        return
        [
            new TtsModelDto
            {
                ModelId = "Audio8-TTS-Preview-0.6b",
                Name = "Audio8 TTS Preview 0.6B",
                Voices = [defaultVoice, .. customVoices]
            }
        ];
    }

    /// <inheritdoc />
    public Task<decimal?> GetBalanceUsdAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<decimal?>(null);

    private async Task<PcmResult> GeneratePcmChunkAsync(
        string text,
        VoiceReference? voiceReference,
        CancellationToken cancellationToken)
    {
        using var formData = new MultipartFormDataContent();
        formData.Add(new StringContent(text), "text");

        if (voiceReference is not null)
        {
            formData.Add(new StringContent(voiceReference.Transcript), "ref_text");
            await using var referenceWavStream = File.OpenRead(voiceReference.WavPath);
            using var wavContent = new StreamContent(referenceWavStream);
            wavContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            formData.Add(wavContent, "reference_wav", Path.GetFileName(voiceReference.WavPath));

            return await PostTtsAsync(formData, cancellationToken);
        }

        return await PostTtsAsync(formData, cancellationToken);
    }

    private async Task<PcmResult> PostTtsAsync(
        MultipartFormDataContent formData,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsync(
            await CreateRequestUriAsync("tts", cancellationToken),
            formData,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var sampleRate = response.Headers.TryGetValues("X-Sample-Rate", out var values)
                         && int.TryParse(values.FirstOrDefault(), out var parsedSampleRate)
            ? parsedSampleRate
            : _sampleRate;

        return new PcmResult(
            await response.Content.ReadAsByteArrayAsync(cancellationToken),
            sampleRate);
    }

    private async Task<VoiceReference?> ResolveVoiceReferenceAsync(
        string? voiceId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(voiceId)
            || voiceId.Equals(_defaultVoiceId, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!Guid.TryParse(voiceId, out var id))
        {
            throw new ApiException(ErrorCodes.BadRequest, $"Invalid Audio8 voice ID: {voiceId}");
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var transcript = await dbContext.Voices
            .AsNoTracking()
            .Where(voice => voice.Id == id)
            .Select(voice => voice.Transcript)
            .SingleOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(transcript))
        {
            throw new ApiException(
                ErrorCodes.BadRequest,
                $"Voice {voiceId} needs an exact sample transcript before it can be used with Audio8.");
        }

        var wavPath = Path.Combine(_voicesFolder, $"{id}.wav");

        if (!File.Exists(wavPath))
        {
            throw new ApiException(
                ErrorCodes.InvalidFile,
                $"Voice sample file was not found for voice ID: {voiceId}");
        }

        return new VoiceReference(wavPath, transcript.Trim());
    }

    private async Task<Uri> CreateRequestUriAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        var config = await _integrationsService.GetConfigAsync(cancellationToken);
        var baseUri = ProviderBaseUrlHelper.NormalizeHttpBaseUri(
            config.Audio8BaseUrl,
            IntegrationsConfig.DefaultAudio8BaseUrl,
            "Audio8");

        return ProviderBaseUrlHelper.CreateRequestUri(baseUri, relativePath);
    }

    private sealed record VoiceReference(string WavPath, string Transcript);

    private sealed record PcmResult(byte[] Pcm, int SampleRate);
}
