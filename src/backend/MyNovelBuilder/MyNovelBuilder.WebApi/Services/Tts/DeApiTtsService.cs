using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MyNovelBuilder.WebApi.Attributes;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;

namespace MyNovelBuilder.WebApi.Services.Tts;

/// <summary>
/// Text-to-Speech service for DeAPI.
/// </summary>
[RegisterKeyedService(TtsProvider.DeApi, useHttpClient: true)]
public class DeApiTtsService : ITtsService
{
    private readonly ILogger<DeApiTtsService> _logger;
    private readonly HttpClient _httpClient;
    private readonly IIntegrationsService _integrationsService;

    /// <inheritdoc />
    public bool SupportsEmphasisTags => false;

    /// <inheritdoc />
    public AudioFormat OutputAudioFormat => AudioFormat.Wav;
    
    /// <summary></summary>
    public DeApiTtsService(
        ILogger<DeApiTtsService> logger,
        HttpClient httpClient,
        IIntegrationsService integrationsService)
    {
        _logger = logger;
        _httpClient = httpClient;
        _integrationsService = integrationsService;
        _httpClient.BaseAddress = new Uri("https://api.deapi.ai/api/v1/client/");
    }

    private async Task<string> GetApiKeyAsync(CancellationToken cancellationToken = default)
    {
        var config = await _integrationsService.GetConfigAsync(cancellationToken);
        var apiKey = config.DeApiApiKey;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ApiException(ErrorCodes.MissingOrInvalidServiceCredentials,
                "DeAPI API key is not configured.");
        }
        
        return apiKey;
    }
    
    private async Task<byte[]> PollForResultAsync(
        string requestId,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        var timeout = TimeSpan.FromMinutes(5);
        var startTime = DateTime.UtcNow;

        // Polling
        while (DateTime.UtcNow - startTime < timeout)
        {
            await Task.Delay(2000, cancellationToken);

            var statusRequest = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(_httpClient.BaseAddress!, $"request-status/{requestId}")
            };
            statusRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");

            using var statusResponse = await _httpClient.SendAsync(statusRequest, cancellationToken);
            var statusJson = await statusResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!statusResponse.IsSuccessStatusCode)
            {
                _logger.LogError("DeAPI generation status check failed. Status code: {StatusCode}, Response: {Response}",
                    statusResponse.StatusCode, statusJson);

                throw new ApiException(ErrorCodes.ExternalServiceError,
                    "Failed to check generation status with DeAPI.");
            }

            var statusObject = JsonNode.Parse(statusJson)!;

            // status can be "pending", "processing", "done" or "error"
            var status = statusObject["data"]!["status"]!.GetValue<string>();

            if (status == "done")
            {
                var imageUrl = statusObject["data"]!["result_url"]!.GetValue<string>();
                return await _httpClient.GetByteArrayAsync(imageUrl, cancellationToken);
            }

            if (status == "error")
            {
                _logger.LogError("DeAPI generation failed. Request ID: {RequestId}, Status response: {StatusResponse}",
                    requestId, statusJson);

                throw new ApiException(ErrorCodes.ExternalServiceError,
                    "Generation failed with DeAPI.");
            }
        }

        _logger.LogError("DeAPI generation timed out. Request ID: {RequestId}", requestId);
        throw new ApiException(ErrorCodes.ExternalServiceError,
            "Generation with DeAPI timed out.");
    }
    
    /// <inheritdoc />
    public async Task<byte[]> GenerateAudioAsync(TtsRequestDto request, CancellationToken cancellationToken = default)
    {
        var apiKey = await GetApiKeyAsync(cancellationToken);
        var config = await _integrationsService.GetConfigAsync(cancellationToken);
        
        var voiceParts = config.TtsVoiceId.Split('/');
        
        if (voiceParts.Length != 3)
        {
            throw new ApiException(ErrorCodes.MissingOrInvalidServiceCredentials,
                "DeAPI TTS voice ID is not in the correct format. Expected format: {modelSlug}/{languageCode}/{voiceSlug}");
        }
        
        var modelSlug = voiceParts[0];
        var languageCode = voiceParts[1];
        var voiceSlug = voiceParts[2];
        
        // TODO: This endpoint only supports text up to 1000 characters!
        //  Use TextChunker to split into multiple requests, maybe use a stream like
        //  for UnrealSpeech
        var httpRequest = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri(_httpClient.BaseAddress!, "txt2audio"),
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    model = modelSlug,
                    text = request.Message,
                    voice = voiceSlug,
                    lang = languageCode,
                    speed = 1,
                    format = "wav",
                    sample_rate = 24000
                }), Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("DeAPI TTS generation failed. Status code: {StatusCode}, Response: {Response}",
                response.StatusCode, jsonResponse);
            throw new ApiException(ErrorCodes.ExternalServiceError,
                "TTS generation failed with DeAPI.");
        }

        var responseObject = JsonNode.Parse(jsonResponse)!;
        var requestId = responseObject["data"]!["request_id"]!.GetValue<string>();

        return await PollForResultAsync(requestId, apiKey, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Stream> GenerateAudioStreamAsync(TtsRequestDto request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TtsVoiceDto>> GetVoicesAsync(CancellationToken cancellationToken = default)
    {
        var apiKey = await GetApiKeyAsync(cancellationToken);
        
        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(_httpClient.BaseAddress!, "models")
        };
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
        
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("DeAPI get models failed. Status code: {StatusCode}, Response: {Response}",
                response.StatusCode, jsonResponse);
            throw new ApiException(ErrorCodes.ExternalServiceError,
                "Failed to get image generation models from DeAPI.");
        }
        
        var responseObject = JsonNode.Parse(jsonResponse)!;
        var voices = new List<TtsVoiceDto>();
        
        foreach (var m in responseObject["data"]!.AsArray())
        {
            var modelSlug = m!["slug"]!.GetValue<string>();
            var modelName = m["name"]!.GetValue<string>();
            var inferenceTypes = m["inference_types"]?.AsArray().Select(t => t!.GetValue<string>()).ToList() ?? [];
            
            if (!inferenceTypes.Contains("txt2audio"))
            {
                continue;
            }

            var languages = m["languages"]?.AsArray() ?? [];

            foreach (var language in languages)
            {
                var languageName = language!["name"]!.GetValue<string>();
                var languageCode = language["slug"]!.GetValue<string>();
                var languageVoices = language["voices"]?.AsArray() ?? [];
                
                foreach (var voice in languageVoices)
                {
                    var voiceName = voice!["name"]!.GetValue<string>();
                    var voiceSlug = voice["slug"]!.GetValue<string>();
                    
                    voices.Add(new TtsVoiceDto
                    {
                        VoiceId = $"{modelSlug}/{languageCode}/{voiceSlug}",
                        Name = $"{modelName} - {languageName} - {voiceName}"
                    });
                }
            }
        }
        
        return voices;
    }
}
