using ElevenLabs;
using ElevenLabs.Models;
using ElevenLabs.TextToSpeech;
using ElevenLabs.Voices;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;

using MyNovelBuilder.WebApi.Attributes;

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
    public AudioFormat OutputAudioFormat => AudioFormat.Mp3;

    /// <summary></summary>
    public ElevenLabsTtsService(
        IIntegrationsService integrationsService)
    {
        _integrationsService = integrationsService;
    }
    
    /// <inheritdoc />
    public async Task<byte[]> GenerateAudioAsync(TtsRequestDto request)
    {
        var config = await _integrationsService.GetConfigAsync();
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
                model: new Model("eleven_v3")));

        return voiceClip.ClipData.ToArray();
    }
    
    /// <inheritdoc />
    public async Task<Stream> GenerateAudioStreamAsync(TtsRequestDto request)
    {
        var config = await _integrationsService.GetConfigAsync();
        var apiKey = config.ElevenLabsApiKey;
        
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ApiException(
                ErrorCodes.MissingOrInvalidServiceCredentials,
                "ElevenLabs API key is missing.");
        }
        
        var client = new ElevenLabsClient(apiKey);
        
        var ms = new MemoryStream();
        await client.TextToSpeechEndpoint.TextToSpeechAsync(
            new TextToSpeechRequest(
                new Voice(config.TtsVoiceId, string.Empty),
                request.Message,
                model: new Model("eleven_v3")),
            partialClipCallback: async partialClip =>
            {
                await ms.WriteAsync(partialClip.ClipData);
            });

        return ms;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TtsVoiceDto>> GetVoicesAsync()
    {
        var config = await _integrationsService.GetConfigAsync();
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
        });
        
        return voices.Voices.Select(v => new TtsVoiceDto
        {
            VoiceId = v.Id,
            Name = v.Name,
        });
    }
}
