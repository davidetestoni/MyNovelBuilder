using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyNovelBuilder.WebApi.Attributes;
using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Helpers;
using MyNovelBuilder.WebApi.Models.Prompts;
using MyNovelBuilder.WebApi.Models.Tts;
using MyNovelBuilder.WebApi.Options;
using MyNovelBuilder.WebApi.Services.TextGeneration;
using NAudio.Wave;

namespace MyNovelBuilder.WebApi.Services.Tts;

/// <summary>
/// Text-to-speech service for OmniVoice.
/// </summary>
[RegisterKeyedService(TtsProvider.OmniVoice, useHttpClient: true)]
public class OmniVoiceTtsService : ITtsService
{
    private readonly HttpClient _httpClient;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly string _voicesFolder;
    private const int _maxChunkLength = 500;
    private const int _sampleRate = 24000;
    private const string _defaultLanguageCode = "en";
    private const string _defaultModelId = "k2-fsa/OmniVoice";
    private const string _defaultModelName = "OmniVoice";
    // TODO: Make the emphasis model configurable.
    private const string _emphasisModel = "anthropic/claude-sonnet-4";
    // TODO: Make the emphasis prompt user-configurable.
    private const string _emphasisPrompt =
        """
        You are an audio labeling specialist for OmniVoice.
        You will be given a text that needs to be enriched with the most fitting emphasis tags in the appropriate places. The goal is to insert tags where it makes sense in the text without altering the existing text in any other way.
        You MUST reply with ONLY the enriched text (nothing else).
        
        You may ONLY use these exact tags:
        [laughter]
        [sigh]
        [confirmation-en]
        [question-en]
        [question-ah]
        [question-oh]
        [question-ei]
        [question-yi]
        [surprise-ah]
        [surprise-oh]
        [surprise-wa]
        [surprise-yo]
        [dissatisfaction-hnn]
        
        Rules:
        Do not invent any other tags.
        Do not rewrite, paraphrase, translate, summarize, or reorder the text.
        Do not overdo it. Add tags sparingly and only where they clearly improve performance.
        Preserve all original words, punctuation, spacing, and line breaks except where a tag is inserted.
        If no tag is clearly appropriate, return the text unchanged.
        
        Prefer:
        [laughter] for amusement or light mockery.
        [sigh] for weariness, relief, resignation, or sadness.
        [confirmation-en] for affirmative responses or clear agreement.
        [question-*] only where the line should sound interrogative.
        [surprise-*] only for short bursts of surprise or sudden realization.
        [dissatisfaction-hnn] for mild displeasure, doubt, or disapproval.
        
        Here's an example of a base text:
        Are you really leaving already? Oh, I didn't expect that. Well, if that's your choice, then fine.
        
        and its enriched version:
        [question-en] Are you really leaving already? [surprise-oh] Oh, I didn't expect that. [dissatisfaction-hnn] Well, if that's your choice, then fine.
        """;

    /// <inheritdoc />
    public AudioFormat OutputAudioFormat => AudioFormat.Wav;

    /// <inheritdoc />
    public bool SupportsTextEmphasis(string? modelId) => true;

    /// <summary></summary>
    public OmniVoiceTtsService(
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

    /// <inheritdoc />
    public async Task<string> EmphasizeTextAsync(
        TtsRequest request,
        Func<CancellationToken, ValueTask<ITextGenerationService>> textGenerationServiceFactory,
        CancellationToken cancellationToken = default)
    {
        var textGenerationService = await textGenerationServiceFactory(cancellationToken);
        return await textGenerationService.GenerateAsync(
            _emphasisModel,
            [
                new PromptMessage
                {
                    Role = PromptMessageRole.System,
                    Message = _emphasisPrompt
                },
                new PromptMessage
                {
                    Role = PromptMessageRole.User,
                    Message = $"Here's the text that needs to be enriched:\n{request.Message}"
                }
            ],
            cancellationToken: cancellationToken);
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
            using var response = await GenerateChunkAsync(
                chunk,
                languageCode,
                referenceWavPath,
                cancellationToken);

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
                    using var response = await GenerateChunkAsync(
                        chunk,
                        languageCode,
                        referenceWavPath,
                        ct);

                    var pcmChunk = await response.Content.ReadAsByteArrayAsync(ct);
                    await writeAsync(pcmChunk);
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
                ModelId = _defaultModelId,
                Name = _defaultModelName,
                Voices = customVoices
            }
        ];
    }

    /// <inheritdoc />
    public Task<decimal?> GetBalanceUsdAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<decimal?>(null);

    private async Task<HttpResponseMessage> GenerateChunkAsync(
        string chunk,
        string languageCode,
        string? referenceWavPath,
        CancellationToken cancellationToken)
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

            var responseWithReference = await _httpClient.PostAsync(
                "tts",
                formData,
                cancellationToken);
            responseWithReference.EnsureSuccessStatusCode();
            return responseWithReference;
        }

        var response = await _httpClient.PostAsync(
            "tts",
            formData,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return response;
    }

    private string? GetReferenceWavPath(string? voiceId)
    {
        if (!Guid.TryParse(voiceId, out var id))
        {
            throw new ApiException(
                ErrorCodes.BadRequest,
                $"Invalid OmniVoice voice ID: {voiceId}");
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
            ? MapToOmniVoiceLanguageCode(voiceLanguage.Value)
            : _defaultLanguageCode;
    }

    private static string MapToOmniVoiceLanguageCode(WritingLanguage language)
    {
        return language switch
        {
            WritingLanguage.English => "en",
            WritingLanguage.Italian => "it",
            WritingLanguage.French => "fr",
            WritingLanguage.Spanish => "es",
            WritingLanguage.German => "de",
            WritingLanguage.Russian => "ru",
            _ => _defaultLanguageCode
        };
    }
}
