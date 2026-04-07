using ElevenLabs;
using ElevenLabs.Models;
using ElevenLabs.TextToSpeech;
using ElevenLabs.Voices;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Models.Prompts;
using MyNovelBuilder.WebApi.Models.Tts;

using MyNovelBuilder.WebApi.Attributes;
using MyNovelBuilder.WebApi.Helpers;
using MyNovelBuilder.WebApi.Services.TextGeneration;

namespace MyNovelBuilder.WebApi.Services.Tts;

/// <summary>
/// Service for generating audio using ElevenLabs TTS.
/// </summary>
[RegisterKeyedService(TtsProvider.ElevenLabs)]
public class ElevenLabsTtsService : ITtsService
{
    private readonly IIntegrationsService _integrationsService;
    // TODO: Make the emphasis model configurable.
    private const string _emphasisModel = "anthropic/claude-sonnet-4";
    // TODO: Make the emphasis prompt user-configurable.
    private const string _emphasisPrompt =
        """
        You are an audio labeling specialist.
        You will be given a text that needs to be enriched with the most fitting style tags in the appropriate places. The goal is to insert tags where it makes sense in the text (don't overdo it) without altering the existing text in any other way, to help a narrator know with which tone and pace they need to read different parts of the text.
        You MUST reply with ONLY the enriched text (nothing else).
        
        Tags can be anything that makes sense, you're not limited to just this list, but here are some examples of what can be done:
        Emotional tone: [excited], [nervous], [frustrated], [tired]
        Reactions: [gasp], [sigh], [laughs], [gulps]
        Volume & energy: [whispering], [shouting], [quietly], [loudly]
        Pacing & rhythm: [pauses], [stammers], [rushed]
        
        Don't overdo it, only place tags where it makes sense to use them.
        
        Here's an example of a base text:
        In the ancient land of Eldoria, where skies shimmered and forests, whispered secrets to the wind, lived a dragon named Zephyros. Not the "burn it all down" kind... but he was gentle, wise, with eyes like old stars. Even the birds fell silent when he passed.
        
        and its enriched version
        In the ancient land of Eldoria, where skies shimmered and forests, whispered secrets to the wind, lived a dragon named Zephyros. [sarcastically] Not the "burn it all down" kind... [giggles] but he was gentle, wise, with eyes like old stars. [whispers] Even the birds fell silent when he passed.
        """;
    
    /// <inheritdoc />
    public AudioFormat OutputAudioFormat => AudioFormat.Wav;

    /// <summary></summary>
    public ElevenLabsTtsService(
        IIntegrationsService integrationsService)
    {
        _integrationsService = integrationsService;
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
    
    /// <inheritdoc />
    public async Task<byte[]> GenerateAudioAsync(
        TtsRequest request,
        CancellationToken cancellationToken = default)
    {
        var config = await _integrationsService.GetConfigAsync(cancellationToken);
        var apiKey = config.ElevenLabsApiKey;
        
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ApiException(
                ErrorCodes.MissingOrInvalidServiceCredentials,
                "ElevenLabs API key is missing.");
        }
        
        var client = new ElevenLabsClient(apiKey);

        var voiceClip = await client.TextToSpeechEndpoint.TextToSpeechAsync(
            new TextToSpeechRequest(
                new Voice(request.VoiceId, string.Empty),
                request.Message,
                model: new Model(request.ModelId ?? "eleven_v3"),
                outputFormat: OutputFormat.PCM_24000),
            cancellationToken: cancellationToken);
        
        using var finalAudio = new MemoryStream();
        await using var writer = new NAudio.Wave.WaveFileWriter(finalAudio,
            new NAudio.Wave.WaveFormat(24000, 16, 1));

        await writer.WriteAsync(voiceClip.ClipData.ToArray(), cancellationToken);

        return finalAudio.ToArray();
    }
    
    /// <inheritdoc />
    public async Task<Stream> GenerateAudioStreamAsync(
        TtsRequest request,
        CancellationToken cancellationToken = default)
    {
        var config = await _integrationsService.GetConfigAsync(cancellationToken);
        var apiKey = config.ElevenLabsApiKey;
        
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ApiException(
                ErrorCodes.MissingOrInvalidServiceCredentials,
                "ElevenLabs API key is missing.");
        }
        
        var client = new ElevenLabsClient(apiKey);

        return new PcmWavStreamingStream(
            sampleRate: 24000,
            channels: 1,
            bitsPerSample: 16,
            producer: async (writeAsync, ct) =>
            {
                await client.TextToSpeechEndpoint.TextToSpeechAsync(
                    new TextToSpeechRequest(
                        new Voice(request.VoiceId, string.Empty),
                        request.Message,
                        model: new Model(request.ModelId ?? "eleven_v3"),
                        outputFormat: OutputFormat.PCM_24000),
                    partialClipCallback: partialClip => writeAsync(partialClip.ClipData), cancellationToken: ct);
            },
            ct: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TtsModelDto>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        var config = await _integrationsService.GetConfigAsync(cancellationToken);
        var apiKey = config.ElevenLabsApiKey;
        
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ApiException(
                ErrorCodes.MissingOrInvalidServiceCredentials,
                "ElevenLabs API key is missing.");
        }
        
        var client = new ElevenLabsClient(apiKey);

        var voices = await client.VoicesV2Endpoint.GetVoicesAsync(new VoiceQuery
        {
            PageSize = 100
        }, cancellationToken);

        return
        [
            new TtsModelDto
            {
                ModelId = "eleven_v3",
                Name = "Eleven v3",
                Voices = voices.Voices.Select(v => new TtsVoiceDto
                {
                    VoiceId = v.Id,
                    Name = v.Name,
                    Language = WritingLanguage.English
                })
            }
        ];
    }

    /// <inheritdoc />
    public Task<decimal?> GetBalanceUsdAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<decimal?>(null);

}
