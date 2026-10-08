using System.Net.Http.Headers;
using System.Text.Json;
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
/// Text-to-speech service for the local KittenTTS 2 server.
/// </summary>
[RegisterKeyedService(TtsProvider.KittenTts, useHttpClient: true)]
public class KittenTtsService : ITtsService
{
    private readonly HttpClient _httpClient;
    private readonly IIntegrationsService _integrationsService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly string _voicesFolder;
    private const int _maxChunkLength = 500;

    /// <inheritdoc />
    public AudioFormat OutputAudioFormat => AudioFormat.Wav;

    /// <summary></summary>
    public KittenTtsService(
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

        var voiceReference = request.ExecutionInputs is null ? await ResolveVoiceReferenceAsync(request.VoiceId, cancellationToken) : null;
        var firstChunk = await GeneratePcmChunkAsync(
            textChunks[0], request.VoiceId, voiceReference, cancellationToken, request.ExecutionInputs);
        using var wavStream = new MemoryStream();
        await using (var writer = new WaveFileWriter(wavStream, new WaveFormat(firstChunk.SampleRate, 16, 1)))
        {
            await writer.WriteAsync(firstChunk.Pcm, cancellationToken);
            foreach (var chunk in textChunks.Skip(1))
            {
                var result = await GeneratePcmChunkAsync(chunk, request.VoiceId, voiceReference, cancellationToken, request.ExecutionInputs);
                EnsureSameSampleRate(firstChunk, result);
                await writer.WriteAsync(result.Pcm, cancellationToken);
            }
        }

        return wavStream.ToArray();
    }

    /// <inheritdoc />
    public async Task<Stream> GenerateAudioStreamAsync(
        TtsRequest request,
        CancellationToken cancellationToken = default)
    {
        var textChunks = new TextChunker(_maxChunkLength).ChunkText(request.Message);
        if (textChunks.Count == 0)
        {
            return new MemoryStream();
        }

        var voiceReference = request.ExecutionInputs is null ? await ResolveVoiceReferenceAsync(request.VoiceId, cancellationToken) : null;
        // The first response supplies the sample rate before we emit the WAV header.
        var firstChunk = await GeneratePcmChunkAsync(
            textChunks[0], request.VoiceId, voiceReference, cancellationToken, request.ExecutionInputs);

        return new PcmWavStreamingStream(
            sampleRate: firstChunk.SampleRate,
            channels: 1,
            bitsPerSample: 16,
            producer: async (writeAsync, ct) =>
            {
                await writeAsync(firstChunk.Pcm);
                foreach (var chunk in textChunks.Skip(1))
                {
                    var result = await GeneratePcmChunkAsync(chunk, request.VoiceId, voiceReference, ct, request.ExecutionInputs);
                    EnsureSameSampleRate(firstChunk, result);
                    await writeAsync(result.Pcm);
                }
            },
            ct: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TtsModelDto>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(
            await CreateRequestUriAsync("model", cancellationToken), cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var info = document.RootElement;
        var builtInVoices = info.GetProperty("available_voices").EnumerateArray()
            .Select(voice => new TtsVoiceDto
            {
                VoiceId = voice.GetString()!,
                Name = voice.GetString()!,
                Language = WritingLanguage.English
            }).ToList();

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
            }).ToListAsync(cancellationToken);

        return
        [
            new TtsModelDto
            {
                ModelId = info.GetProperty("checkpoint").GetString()!,
                Name = "KittenTTS 2",
                Voices = [.. builtInVoices, .. customVoices]
            }
        ];
    }

    /// <inheritdoc />
    public Task<decimal?> GetBalanceUsdAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<decimal?>(null);

    private async Task<PcmResult> GeneratePcmChunkAsync(
        string text, string? voiceId, VoiceReference? voiceReference, CancellationToken cancellationToken, TtsExecutionInputs? execution = null)
    {
        using var formData = new MultipartFormDataContent();
        formData.Add(new StringContent(text), "text");
        formData.Add(new StringContent("pcm"), "output_format");

        if (execution?.Voice is { } frozenVoice)
        {
            formData.Add(new StringContent(frozenVoice.Transcript!.Trim()), "ref_text");
            var wavContent = new StreamContent(frozenVoice.OpenRead());
            wavContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            formData.Add(wavContent, "reference_wav", "reference.wav");
        }
        else if (voiceReference is not null)
        {
            formData.Add(new StringContent(voiceReference.Transcript), "ref_text");
            var wavContent = new StreamContent(File.OpenRead(voiceReference.WavPath));
            wavContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            formData.Add(wavContent, "reference_wav", Path.GetFileName(voiceReference.WavPath));
        }
        else if (!string.IsNullOrWhiteSpace(voiceId))
        {
            formData.Add(new StringContent(voiceId), "voice");
        }

        using var response = await _httpClient.PostAsync(
            (execution?.BaseUri is { } uri ? ProviderBaseUrlHelper.CreateRequestUri(uri, "tts") : await CreateRequestUriAsync("tts", cancellationToken)), formData, cancellationToken);
        response.EnsureSuccessStatusCode();
        if (!response.Headers.TryGetValues("X-Sample-Rate", out var values)
            || !int.TryParse(values.FirstOrDefault(), out var sampleRate)
            || sampleRate <= 0)
        {
            throw new InvalidOperationException("KittenTTS did not return a valid X-Sample-Rate header.");
        }

        return new PcmResult(await response.Content.ReadAsByteArrayAsync(cancellationToken), sampleRate);
    }

    private async Task<VoiceReference?> ResolveVoiceReferenceAsync(
        string? voiceId, CancellationToken cancellationToken)
    {
        // Built-in voices use names; saved voice samples use database GUIDs.
        if (!Guid.TryParse(voiceId, out var id))
        {
            return null;
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var transcript = await dbContext.Voices.AsNoTracking()
            .Where(voice => voice.Id == id)
            .Select(voice => voice.Transcript)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(transcript))
        {
            throw new ApiException(ErrorCodes.BadRequest,
                $"Voice {voiceId} needs an exact sample transcript before it can be used with KittenTTS.");
        }

        var wavPath = Path.Combine(_voicesFolder, $"{id}.wav");
        if (!File.Exists(wavPath))
        {
            throw new ApiException(ErrorCodes.InvalidFile,
                $"Voice sample file was not found for voice ID: {voiceId}");
        }

        return new VoiceReference(wavPath, transcript.Trim());
    }

    private async Task<Uri> CreateRequestUriAsync(string relativePath, CancellationToken cancellationToken)
    {
        var config = await _integrationsService.GetConfigAsync(cancellationToken);
        var baseUri = ProviderBaseUrlHelper.NormalizeHttpBaseUri(
            config.KittenTtsBaseUrl, IntegrationsConfig.DefaultKittenTtsBaseUrl, "KittenTTS");
        return ProviderBaseUrlHelper.CreateRequestUri(baseUri, relativePath);
    }

    private static void EnsureSameSampleRate(PcmResult first, PcmResult next)
    {
        if (first.SampleRate != next.SampleRate)
        {
            throw new InvalidOperationException("KittenTTS changed the sample rate between audio chunks.");
        }
    }

    private sealed record VoiceReference(string WavPath, string Transcript);
    private sealed record PcmResult(byte[] Pcm, int SampleRate);
}
