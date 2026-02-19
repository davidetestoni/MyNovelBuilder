using ElevenLabs;
using ElevenLabs.Models;
using ElevenLabs.TextToSpeech;
using ElevenLabs.Voices;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;

using MyNovelBuilder.WebApi.Attributes;
using MyNovelBuilder.WebApi.Helpers;

namespace MyNovelBuilder.WebApi.Services.Tts;

/// <summary>
/// Service for generating audio using ElevenLabs TTS.
/// </summary>
[RegisterKeyedService(TtsProvider.ElevenLabs)]
public class ElevenLabsTtsService : ITtsService
{
    private readonly IIntegrationsService _integrationsService;

    /// <inheritdoc />
    public bool SupportsEmphasisTags => true;
    
    /// <inheritdoc />
    public AudioFormat OutputAudioFormat => AudioFormat.Wav;

    /// <summary></summary>
    public ElevenLabsTtsService(
        IIntegrationsService integrationsService)
    {
        _integrationsService = integrationsService;
    }
    
    /// <inheritdoc />
    public async Task<byte[]> GenerateAudioAsync(
        TtsRequestDto request,
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
                new Voice(config.TtsVoiceId, string.Empty),
                request.Message,
                model: new Model("eleven_v3"),
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
        TtsRequestDto request,
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
                        new Voice(config.TtsVoiceId, string.Empty),
                        request.Message,
                        model: new Model("eleven_v3"),
                        outputFormat: OutputFormat.PCM_24000),
                    partialClipCallback: partialClip => writeAsync(partialClip.ClipData), cancellationToken: ct);
            },
            ct: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TtsVoiceDto>> GetVoicesAsync(CancellationToken cancellationToken = default)
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
        
        return voices.Voices.Select(v => new TtsVoiceDto
        {
            VoiceId = v.Id,
            Name = v.Name,
        });
    }

}
