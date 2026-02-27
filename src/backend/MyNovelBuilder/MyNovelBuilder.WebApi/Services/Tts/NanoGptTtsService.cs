using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Globalization;
using MyNovelBuilder.WebApi.Attributes;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Helpers;
using MyNovelBuilder.WebApi.Models.Tts;

namespace MyNovelBuilder.WebApi.Services.Tts;

/// <summary>
/// Text-to-speech service for NanoGPT.
/// </summary>
[RegisterKeyedService(TtsProvider.NanoGpt, useHttpClient: true)]
public class NanoGptTtsService : ITtsService
{
    private static readonly TimeSpan _pollDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan _pollTimeout = TimeSpan.FromMinutes(5);
    private const string _elevenLabsV3Model = "Elevenlabs-V3";

    private readonly HttpClient _httpClient;
    private readonly ILogger<NanoGptTtsService> _logger;
    private readonly IIntegrationsService _integrationsService;
    
    /// <inheritdoc />
    public bool SupportsEmphasisTags(string voiceId)
    {
        var model = voiceId.Split('/', 2)[0];
        return string.Equals(model, _elevenLabsV3Model, StringComparison.OrdinalIgnoreCase);
    }
    
    /// <inheritdoc />
    public AudioFormat OutputAudioFormat => AudioFormat.Wav;

    /// <summary></summary>
    public NanoGptTtsService(
        HttpClient httpClient,
        ILogger<NanoGptTtsService> logger,
        IIntegrationsService integrationsService)
    {
        _httpClient = httpClient;
        _logger = logger;
        _integrationsService = integrationsService;
        _httpClient.BaseAddress = new Uri("https://nano-gpt.com/api/");
    }
    
    /// <inheritdoc />
    public async Task<byte[]> GenerateAudioAsync(TtsRequest request, CancellationToken cancellationToken = default)
    {
        var apiKey = await GetApiKeyAsync(cancellationToken);
        if (!TryParseVoiceId(request.VoiceId, out var model, out var voice))
        {
            throw new ApiException(
                ErrorCodes.MissingOrInvalidServiceCredentials,
                "NanoGPT TTS voice ID is not in the correct format. Expected format: {model}/{voice}");
        }

        var payload = new
        {
            model,
            input = request.Message,
            voice,
            response_format = "mp3",
        };

        var httpRequest = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri(_httpClient.BaseAddress!, "v1/audio/speech"),
            Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json")
        };
        httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorResponse = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "NanoGPT TTS generation failed. Status code: {StatusCode}, Response: {Response}",
                response.StatusCode,
                errorResponse);

            throw new ApiException(
                ErrorCodes.ExternalServiceError,
                GetErrorMessage(errorResponse) ?? "TTS generation failed with NanoGPT.");
        }
        
        var contentType = response.Content.Headers.ContentType?.MediaType;
        
        if (string.Equals(contentType, "application/json", StringComparison.OrdinalIgnoreCase))
        {
            var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
            var jsonObject = JsonNode.Parse(jsonResponse)?.AsObject()
                             ?? throw new ApiException(
                                 ErrorCodes.ExternalServiceError,
                                 "NanoGPT returned an invalid JSON response for TTS.");

            return await GetAudioFromJsonResponseAsync(jsonObject, jsonResponse, model, apiKey, cancellationToken);
        }

        var audioBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return await AudioConversionHelper.ConvertMp3ToWavBytesAsync(audioBytes, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Stream> GenerateAudioStreamAsync(TtsRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TtsVoiceDto>> GetVoicesAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync("v1/audio-models?detailed=true", cancellationToken);
        var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "NanoGPT audio models request failed. Status code: {StatusCode}, Response: {Response}",
                response.StatusCode,
                jsonResponse);

            throw new ApiException(
                ErrorCodes.ExternalServiceError,
                GetErrorMessage(jsonResponse) ?? "Failed to get TTS models from NanoGPT.");
        }

        var responseObject = JsonNode.Parse(jsonResponse)?.AsObject() 
                             ?? throw new ApiException(
                                 ErrorCodes.ExternalServiceError,
                                 "NanoGPT returned an invalid JSON response for audio models.");

        var voices = new List<TtsVoiceDto>();
        var models = responseObject["data"]?.AsArray()
                     ?? throw new ApiException(
                         ErrorCodes.ExternalServiceError,
                         "NanoGPT audio models response did not include a data array.");

        foreach (var modelNode in models)
        {
            if (modelNode is not JsonObject modelObject || modelObject["capabilities"]?["text_to_speech"]?.GetValue<bool>() != true)
            {
                continue;
            }

            var modelId = modelObject["id"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(modelId))
            {
                continue;
            }

            var modelName = modelObject["name"]?.GetValue<string>() ?? modelId;
            var providerName = modelObject["owned_by"]?.GetValue<string>() ?? "NanoGPT";
            var modelVoices = modelObject["supported_parameters"]?["voices"]?.AsArray();

            if (modelVoices is null)
            {
                continue;
            }

            foreach (var voiceNode in modelVoices)
            {
                var voice = voiceNode?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(voice))
                {
                    continue;
                }

                voices.Add(new TtsVoiceDto
                {
                    VoiceId = $"{modelId}/{voice}",
                    Name = $"{providerName} - {modelName} - {voice}",
                    Language = WritingLanguage.English
                });
            }
        }

        return voices
            .OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(v => v.VoiceId, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<decimal?> GetBalanceUsdAsync(CancellationToken cancellationToken = default)
    {
        var apiKey = await GetApiKeyAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, "check-balance");
        request.Headers.TryAddWithoutValidation("x-api-key", apiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new ApiException(
                ErrorCodes.ExternalServiceError,
                $"NanoGPT balance request failed with status {(int)response.StatusCode}.");
        }

        var balanceValue = JsonNode.Parse(json)?["usd_balance"]?.ToString();
        if (!decimal.TryParse(balanceValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedBalance))
        {
            throw new ApiException(
                ErrorCodes.ExternalServiceError,
                "NanoGPT balance response did not include a valid usd_balance value.");
        }

        return parsedBalance;
    }

    private async Task<string> GetApiKeyAsync(CancellationToken cancellationToken = default)
    {
        var config = await _integrationsService.GetConfigAsync(cancellationToken);
        var apiKey = config.NanoGptApiKey;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ApiException(
                ErrorCodes.MissingOrInvalidServiceCredentials,
                "NanoGPT API key is not configured.");
        }

        return apiKey;
    }

    private static bool TryParseVoiceId(string? voiceId, out string model, out string voice)
    {
        model = string.Empty;
        voice = string.Empty;

        if (string.IsNullOrWhiteSpace(voiceId))
        {
            return false;
        }

        var voiceParts = voiceId.Split('/', 2);
        if (voiceParts.Length != 2)
        {
            return false;
        }

        model = voiceParts[0];
        voice = voiceParts[1];
        return !string.IsNullOrWhiteSpace(model) && !string.IsNullOrWhiteSpace(voice);
    }

    private static string? GetErrorMessage(string errorResponse)
    {
        if (string.IsNullOrWhiteSpace(errorResponse))
        {
            return null;
        }

        try
        {
            var errorObject = JsonNode.Parse(errorResponse);
            return errorObject?["error"]?["message"]?.GetValue<string>()
                   ?? errorObject?["message"]?.GetValue<string>();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<byte[]> GetAudioFromJsonResponseAsync(
        JsonObject jsonObject,
        string rawResponse,
        string requestedModel,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var status = jsonObject["status"]?.GetValue<string>();
        var audioUrl = jsonObject["audioUrl"]?.GetValue<string>();
        var cost = jsonObject["cost"]?.GetValue<decimal?>();
        var paymentSource = jsonObject["paymentSource"]?.GetValue<string>();

        if (!string.IsNullOrWhiteSpace(audioUrl))
        {
            LogCost(requestedModel, cost, paymentSource);
            return await DownloadAudioAsync(audioUrl, cancellationToken);
        }

        if (string.Equals(status, "pending", StringComparison.OrdinalIgnoreCase))
        {
            var runId = jsonObject["runId"]?.GetValue<string>();
            var model = jsonObject["model"]?.GetValue<string>();

            if (string.IsNullOrWhiteSpace(runId) || string.IsNullOrWhiteSpace(model))
            {
                _logger.LogError(
                    "NanoGPT TTS returned pending status without required polling fields. Response: {Response}",
                    rawResponse);

                throw new ApiException(
                    ErrorCodes.ExternalServiceError,
                    "NanoGPT returned an incomplete pending TTS response.");
            }

            var isApiRequest = jsonObject["isApiRequest"]?.GetValue<bool?>();

            LogCost(model, cost, paymentSource);

            return await PollForAudioAsync(
                runId,
                model,
                apiKey,
                cost,
                paymentSource,
                isApiRequest,
                cancellationToken);
        }

        if (string.Equals(status, "error", StringComparison.OrdinalIgnoreCase))
        {
            var errorMessage = jsonObject["error"]?.GetValue<string>()
                               ?? GetErrorMessage(rawResponse)
                               ?? "TTS generation failed with NanoGPT.";

            _logger.LogError("NanoGPT TTS generation failed. Response: {Response}", rawResponse);
            throw new ApiException(ErrorCodes.ExternalServiceError, errorMessage);
        }

        _logger.LogError("NanoGPT TTS returned JSON without audioUrl. Response: {Response}", rawResponse);
        throw new ApiException(
            ErrorCodes.ExternalServiceError,
            "NanoGPT returned an unexpected JSON response for TTS.");
    }

    private async Task<byte[]> PollForAudioAsync(
        string runId,
        string model,
        string apiKey,
        decimal? cost,
        string? paymentSource,
        bool? isApiRequest,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;

        while (DateTime.UtcNow - startedAt < _pollTimeout)
        {
            await Task.Delay(_pollDelay, cancellationToken);

            using var statusRequest = new HttpRequestMessage(
                HttpMethod.Get,
                BuildStatusUri(runId, model, cost, paymentSource, isApiRequest));
            statusRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
            statusRequest.Headers.TryAddWithoutValidation("x-api-key", apiKey);

            using var statusResponse = await _httpClient.SendAsync(statusRequest, cancellationToken);
            var statusJson = await statusResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!statusResponse.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "NanoGPT TTS status check failed. Status code: {StatusCode}, Response: {Response}",
                    statusResponse.StatusCode,
                    statusJson);

                throw new ApiException(
                    ErrorCodes.ExternalServiceError,
                    "Failed to check NanoGPT TTS status.");
            }

            var statusObject = JsonNode.Parse(statusJson)?.AsObject()
                               ?? throw new ApiException(
                                   ErrorCodes.ExternalServiceError,
                                   "NanoGPT returned an invalid TTS status response.");
            var status = statusObject["status"]?.GetValue<string>();
            var audioUrl = statusObject["audioUrl"]?.GetValue<string>();
            var responseCost = statusObject["cost"]?.GetValue<decimal?>();
            var responsePaymentSource = statusObject["paymentSource"]?.GetValue<string>();

            if (string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(audioUrl))
            {
                LogCost(model, responseCost, responsePaymentSource);
                return await DownloadAudioAsync(audioUrl, cancellationToken);
            }

            if (string.Equals(status, "error", StringComparison.OrdinalIgnoreCase))
            {
                var errorMessage = statusObject["error"]?.GetValue<string>()
                                   ?? GetErrorMessage(statusJson)
                                   ?? "TTS generation failed with NanoGPT.";

                _logger.LogError(
                    "NanoGPT TTS generation failed during polling. Run ID: {RunId}, Response: {Response}",
                    runId,
                    statusJson);

                throw new ApiException(ErrorCodes.ExternalServiceError, errorMessage);
            }
        }

        _logger.LogError("NanoGPT TTS generation timed out. Run ID: {RunId}, Model: {Model}", runId, model);
        throw new ApiException(
            ErrorCodes.ExternalServiceError,
            "NanoGPT TTS generation timed out.");
    }

    private void LogCost(string model, decimal? cost, string? paymentSource)
    {
        if (!cost.HasValue)
        {
            return;
        }

        _logger.LogInformation(
            "NanoGPT TTS cost for model {Model}: {Cost} {PaymentSource}",
            model,
            cost.Value,
            paymentSource ?? "unknown");
    }

    private async Task<byte[]> DownloadAudioAsync(
        string audioUrl,
        CancellationToken cancellationToken)
    {
        using var audioResponse = await _httpClient.GetAsync(audioUrl, cancellationToken);

        if (!audioResponse.IsSuccessStatusCode)
        {
            var downloadError = await audioResponse.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "NanoGPT TTS audio download failed. Status code: {StatusCode}, Response: {Response}",
                audioResponse.StatusCode,
                downloadError);

            throw new ApiException(
                ErrorCodes.ExternalServiceError,
                "NanoGPT returned an audio URL but the audio download failed.");
        }

        var audioBytes = await audioResponse.Content.ReadAsByteArrayAsync(cancellationToken);
        return await AudioConversionHelper.ConvertMp3ToWavBytesAsync(audioBytes, cancellationToken);
    }

    private static Uri BuildStatusUri(
        string runId,
        string model,
        decimal? cost,
        string? paymentSource,
        bool? isApiRequest)
    {
        var queryParts = new List<string>
        {
            $"runId={Uri.EscapeDataString(runId)}",
            $"model={Uri.EscapeDataString(model)}"
        };

        if (cost.HasValue)
        {
            queryParts.Add($"cost={Uri.EscapeDataString(cost.Value.ToString(CultureInfo.InvariantCulture))}");
        }

        if (!string.IsNullOrWhiteSpace(paymentSource))
        {
            queryParts.Add($"paymentSource={Uri.EscapeDataString(paymentSource)}");
        }

        if (isApiRequest.HasValue)
        {
            queryParts.Add($"isApiRequest={Uri.EscapeDataString(isApiRequest.Value.ToString().ToLowerInvariant())}");
        }

        return new Uri($"tts/status?{string.Join("&", queryParts)}", UriKind.Relative);
    }
}
