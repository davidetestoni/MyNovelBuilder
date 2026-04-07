using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Globalization;
using MyNovelBuilder.WebApi.Attributes;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Helpers;
using MyNovelBuilder.WebApi.Models.Prompts;
using MyNovelBuilder.WebApi.Models.Tts;
using MyNovelBuilder.WebApi.Services.TextGeneration;

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

    private readonly HttpClient _httpClient;
    private readonly ILogger<NanoGptTtsService> _logger;
    private readonly IIntegrationsService _integrationsService;
    
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
    public async Task<string> EmphasizeTextAsync(
        TtsRequest request,
        Func<CancellationToken, ValueTask<ITextGenerationService>> textGenerationServiceFactory,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(request.ModelId, _elevenLabsV3Model, StringComparison.OrdinalIgnoreCase))
        {
            return request.Message;
        }

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
    public async Task<byte[]> GenerateAudioAsync(TtsRequest request, CancellationToken cancellationToken = default)
    {
        var apiKey = await GetApiKeyAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(request.ModelId) || string.IsNullOrWhiteSpace(request.VoiceId))
        {
            throw new ApiException(
                ErrorCodes.MissingOrInvalidServiceCredentials,
                "NanoGPT TTS model and voice IDs are required.");
        }

        var model = request.ModelId;
        var voice = request.VoiceId;

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
    public async Task<IEnumerable<TtsModelDto>> GetModelsAsync(CancellationToken cancellationToken = default)
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

        var modelsById = new List<TtsModelDto>();
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

            var voices = modelVoices
                .Select(voiceNode => voiceNode?.GetValue<string>())
                .Where(voice => !string.IsNullOrWhiteSpace(voice))
                .Select(voice => new TtsVoiceDto
                {
                    VoiceId = voice!,
                    Name = voice!,
                    Language = WritingLanguage.English
                })
                .OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (voices.Count == 0)
            {
                continue;
            }

            modelsById.Add(new TtsModelDto
            {
                ModelId = modelId,
                Name = $"{providerName} - {modelName}",
                Voices = voices
            });
        }

        return modelsById.OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase);
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
