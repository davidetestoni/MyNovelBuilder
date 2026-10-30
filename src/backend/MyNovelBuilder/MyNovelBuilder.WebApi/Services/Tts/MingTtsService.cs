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

/// <summary>Text-to-speech and voice design for the local Ming 3.0 TTS server.</summary>
[RegisterKeyedService(TtsProvider.Ming, useHttpClient: true)]
public class MingTtsService : ITtsService
{
    private readonly HttpClient _httpClient;
    private readonly IIntegrationsService _integrationsService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly string _voicesFolder;
    private const int _maxChunkLength = 500;
    // Ming's other presets speak Chinese, which is not an application writing language.
    private static readonly HashSet<string> _englishSpeakers = ["US_Emma", "UK_George", "UK_Charlotte"];

    /// <inheritdoc />
    public AudioFormat OutputAudioFormat => AudioFormat.Wav;

    /// <inheritdoc />
    public bool SupportsVoiceDesign() => true;

    /// <summary></summary>
    public MingTtsService(
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
        string prompt, WritingLanguage language, string voiceDescription,
        CancellationToken cancellationToken = default)
    {
        using var formData = new MultipartFormDataContent();
        formData.Add(new StringContent(prompt), "prompt");
        formData.Add(new StringContent(MapLanguage(language)), "language");
        formData.Add(new StringContent(voiceDescription.Trim()), "voice_description");
        using var response = await _httpClient.PostAsync(
            await CreateRequestUriAsync("voice-design", cancellationToken), formData, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<byte[]> GenerateAudioAsync(TtsRequest request, CancellationToken cancellationToken = default)
    {
        var chunks = new TextChunker(_maxChunkLength).ChunkText(request.Message);
        if (chunks.Count == 0) return [];

        var voice = await ResolveVoiceAsync(request, cancellationToken);
        var first = await GeneratePcmChunkAsync(chunks[0], voice, request.ExecutionInputs, cancellationToken);
        using var output = new MemoryStream();
        await using (var writer = new WaveFileWriter(output, new WaveFormat(first.SampleRate, 16, 1)))
        {
            await writer.WriteAsync(first.Pcm, cancellationToken);
            foreach (var chunk in chunks.Skip(1))
            {
                var next = await GeneratePcmChunkAsync(chunk, voice, request.ExecutionInputs, cancellationToken);
                EnsureSameSampleRate(first, next);
                await writer.WriteAsync(next.Pcm, cancellationToken);
            }
        }
        return output.ToArray();
    }

    /// <inheritdoc />
    public async Task<Stream> GenerateAudioStreamAsync(TtsRequest request, CancellationToken cancellationToken = default)
    {
        var chunks = new TextChunker(_maxChunkLength).ChunkText(request.Message);
        if (chunks.Count == 0) return new MemoryStream();

        var voice = await ResolveVoiceAsync(request, cancellationToken);
        // Read the first response's rate before emitting the streaming WAV header.
        var first = await GeneratePcmChunkAsync(chunks[0], voice, request.ExecutionInputs, cancellationToken);
        return new PcmWavStreamingStream(
            sampleRate: first.SampleRate,
            channels: 1,
            bitsPerSample: 16,
            producer: async (writeAsync, ct) =>
            {
                await writeAsync(first.Pcm);
                foreach (var chunk in chunks.Skip(1))
                {
                    var next = await GeneratePcmChunkAsync(chunk, voice, request.ExecutionInputs, ct);
                    EnsureSameSampleRate(first, next);
                    await writeAsync(next.Pcm);
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
        var voices = new List<TtsVoiceDto>();
        if (info.GetProperty("voice_clone_enabled").GetBoolean())
        {
            voices.AddRange(info.GetProperty("supported_speakers").EnumerateArray()
                .Select(speaker => speaker.GetString()!)
                .Where(_englishSpeakers.Contains)
                .Select(speaker => new TtsVoiceDto
                {
                    VoiceId = speaker, Name = speaker, Language = WritingLanguage.English
                }));

            using var scope = _serviceScopeFactory.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            voices.AddRange(await database.Voices.AsNoTracking()
                .Where(voice => voice.Language == WritingLanguage.English
                    || voice.Language == WritingLanguage.French || voice.Language == WritingLanguage.Spanish
                    || voice.Language == WritingLanguage.German)
                .OrderBy(voice => voice.Name)
                .Select(voice => new TtsVoiceDto
                {
                    VoiceId = voice.Id.ToString(), Name = voice.Name, Language = voice.Language
                }).ToListAsync(cancellationToken));
        }

        return [new TtsModelDto
        {
            ModelId = info.GetProperty("checkpoint").GetString()!,
            Name = "Ming 3.0 TTS",
            Voices = voices
        }];
    }

    /// <inheritdoc />
    public Task<decimal?> GetBalanceUsdAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<decimal?>(null);

    private async Task<PcmResult> GeneratePcmChunkAsync(
        string text, VoiceInput voice, TtsExecutionInputs? execution, CancellationToken cancellationToken)
    {
        using var formData = new MultipartFormDataContent();
        formData.Add(new StringContent(text), "text");
        formData.Add(new StringContent(voice.Language), "language");
        if (voice.OpenRead is { } openRead)
        {
            var wavContent = new StreamContent(openRead());
            wavContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            formData.Add(wavContent, "reference_wav", "reference.wav");
            if (!string.IsNullOrWhiteSpace(voice.Transcript))
                formData.Add(new StringContent(voice.Transcript.Trim()), "ref_text");
        }
        else
        {
            formData.Add(new StringContent(voice.Speaker!), "speaker");
        }

        using var response = await _httpClient.PostAsync(
            await CreateRequestUriAsync("tts", cancellationToken, execution), formData, cancellationToken);
        response.EnsureSuccessStatusCode();
        if (!response.Headers.TryGetValues("X-Sample-Rate", out var rates)
            || !int.TryParse(rates.FirstOrDefault(), out var rate) || rate <= 0)
            throw new InvalidOperationException("Ming did not return a valid X-Sample-Rate header.");

        return new PcmResult(await response.Content.ReadAsByteArrayAsync(cancellationToken), rate);
    }

    private async Task<VoiceInput> ResolveVoiceAsync(TtsRequest request, CancellationToken cancellationToken)
    {
        if (request.ExecutionInputs?.Voice is { } frozen)
            return new VoiceInput(null, MapLanguage(frozen.Language), frozen.Transcript, frozen.OpenRead);

        if (Guid.TryParse(request.VoiceId, out var id) && request.ExecutionInputs is null)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var voice = await database.Voices.AsNoTracking()
                .Where(voice => voice.Id == id)
                .Select(voice => new { voice.Language, voice.Transcript })
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new ApiException(ErrorCodes.BadRequest, $"Recorded voice {id} is unavailable.");
            var language = MapLanguage(voice.Language);
            var path = Path.Combine(_voicesFolder, $"{id}.wav");
            if (!File.Exists(path))
                throw new ApiException(ErrorCodes.InvalidFile, $"Voice sample file was not found for voice ID: {id}");
            return new VoiceInput(null, language, voice.Transcript, () => File.OpenRead(path));
        }

        if (request.VoiceId is { } speaker && _englishSpeakers.Contains(speaker))
            return new VoiceInput(speaker, "en", null, null);
        throw new ApiException(ErrorCodes.BadRequest, $"Invalid Ming voice ID: {request.VoiceId}");
    }

    private async Task<Uri> CreateRequestUriAsync(
        string relativePath, CancellationToken cancellationToken, TtsExecutionInputs? execution = null)
    {
        if (execution?.BaseUri is { } frozen)
            return ProviderBaseUrlHelper.CreateRequestUri(frozen, relativePath);
        var config = await _integrationsService.GetConfigAsync(cancellationToken);
        var baseUri = ProviderBaseUrlHelper.NormalizeHttpBaseUri(
            config.MingBaseUrl, IntegrationsConfig.DefaultMingBaseUrl, "Ming");
        return ProviderBaseUrlHelper.CreateRequestUri(baseUri, relativePath);
    }

    private static string MapLanguage(WritingLanguage language) => language switch
    {
        WritingLanguage.English => "en",
        WritingLanguage.French => "fr",
        WritingLanguage.Spanish => "es",
        WritingLanguage.German => "de",
        _ => throw new ApiException(ErrorCodes.BadRequest,
            $"Ming does not support {language}. Choose English, French, Spanish, or German.")
    };

    private static void EnsureSameSampleRate(PcmResult first, PcmResult next)
    {
        if (first.SampleRate != next.SampleRate)
            throw new InvalidOperationException("Ming changed the sample rate between audio chunks.");
    }

    private sealed record VoiceInput(string? Speaker, string Language, string? Transcript, Func<Stream>? OpenRead);
    private sealed record PcmResult(byte[] Pcm, int SampleRate);
}
