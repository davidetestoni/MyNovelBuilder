using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MyNovelBuilder.WebApi.Attributes;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Helpers;
using MyNovelBuilder.WebApi.Models.Tts;

namespace MyNovelBuilder.WebApi.Services.Tts;

/// <summary>
/// Text-to-speech service for OpenRouter.
/// </summary>
[RegisterKeyedService(TtsProvider.OpenRouter, useHttpClient: true)]
public class OpenRouterTtsService : ITtsService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenRouterTtsService> _logger;
    private readonly IIntegrationsService _integrationsService;

    /// <inheritdoc />
    public AudioFormat OutputAudioFormat => AudioFormat.Wav;

    /// <summary></summary>
    public OpenRouterTtsService(
        HttpClient httpClient,
        ILogger<OpenRouterTtsService> logger,
        IIntegrationsService integrationsService)
    {
        _httpClient = httpClient;
        _logger = logger;
        _integrationsService = integrationsService;
        _httpClient.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
    }

    /// <inheritdoc />
    public async Task<byte[]> GenerateAudioAsync(
        TtsRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendSpeechRequestAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await using var mp3Stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var wavStream = await AudioConversionHelper.ConvertMp3ToWavStreamAsync(mp3Stream, cancellationToken);
        using var buffer = new MemoryStream();
        await wavStream.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    /// <inheritdoc />
    public async Task<Stream> GenerateAudioStreamAsync(
        TtsRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await SendSpeechRequestAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        try
        {
            var mp3Stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await Mp3WavStreamingStream.CreateAsync(mp3Stream, response.Dispose, cancellationToken);
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TtsModelDto>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        var apiKey = await GetApiKeyAsync(cancellationToken);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, "models?output_modalities=speech");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "OpenRouter TTS models request failed. Status code: {StatusCode}, Response: {Response}",
                response.StatusCode,
                jsonResponse);

            throw new ApiException(
                ErrorCodes.ExternalServiceError,
                GetErrorMessage(jsonResponse) ?? "Failed to get TTS models from OpenRouter.");
        }

        var root = JsonNode.Parse(jsonResponse)?.AsObject()
                   ?? throw new ApiException(
                       ErrorCodes.ExternalServiceError,
                       "OpenRouter returned an invalid JSON response for TTS models.");

        var models = root["data"]?.AsArray()
                     ?? throw new ApiException(
                         ErrorCodes.ExternalServiceError,
                         "OpenRouter TTS models response did not include a data array.");

        return models
            .Where(IsTtsModel)
            .Select(modelNode =>
            {
                var modelId = modelNode?["id"]?.GetValue<string>() ?? string.Empty;
                var modelName = modelNode?["name"]?.GetValue<string>() ?? modelId;

                return new TtsModelDto
                {
                    ModelId = modelId,
                    Name = modelName,
                    Voices = GetVoices(modelNode)
                };
            })
            .OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <inheritdoc />
    public Task<decimal?> GetBalanceUsdAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<decimal?>(null);

    private async Task<HttpResponseMessage> SendSpeechRequestAsync(
        TtsRequest request,
        HttpCompletionOption completionOption,
        CancellationToken cancellationToken)
    {
        var apiKey = await GetApiKeyAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(request.ModelId) || string.IsNullOrWhiteSpace(request.VoiceId))
        {
            throw new ApiException(
                ErrorCodes.MissingOrInvalidServiceCredentials,
                "OpenRouter TTS model and voice IDs are required.");
        }

        using var httpRequest = CreateSpeechRequest(request, apiKey);
        var response = await _httpClient.SendAsync(httpRequest, completionOption, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var errorResponse = await response.Content.ReadAsStringAsync(cancellationToken);
        using var failedResponse = response;

        _logger.LogError(
            "OpenRouter TTS generation failed. Status code: {StatusCode}, Model: {ModelId}, Voice: {VoiceId}, "
            + "Format: {AudioFormat}, Input characters: {InputCharacters}, Input UTF-8 bytes: {InputUtf8Bytes}, "
            + "Generation ID: {GenerationId}, Request ID: {RequestId}, CF-Ray: {CfRay}, Response: {Response}",
            response.StatusCode, request.ModelId, request.VoiceId, "mp3", request.Message.Length,
            Encoding.UTF8.GetByteCount(request.Message),
            GetHeader(response, "X-Generation-Id"), GetHeader(response, "X-Request-Id"), GetHeader(response, "CF-Ray"),
            errorResponse);

        throw new ApiException(
            ErrorCodes.ExternalServiceError,
            GetErrorMessage(errorResponse) ?? "TTS generation failed with OpenRouter.");
    }

    private static HttpRequestMessage CreateSpeechRequest(TtsRequest request, string apiKey)
    {
        var payload = new
        {
            input = request.Message,
            model = request.ModelId,
            voice = request.VoiceId,
            // Raw PCM has no embedded format, and OpenRouter's rate/channels headers
            // are optional. MP3 carries the format even when those headers are absent.
            response_format = "mp3"
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "audio/speech")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpRequest.Headers.Add("X-OpenRouter-Metadata", "enabled");
        return httpRequest;
    }

    private async Task<string> GetApiKeyAsync(CancellationToken cancellationToken = default)
    {
        var config = await _integrationsService.GetConfigAsync(cancellationToken);
        var apiKey = config.OpenRouterApiKey;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ApiException(
                ErrorCodes.MissingOrInvalidServiceCredentials,
                "OpenRouter API key is missing in integrations configuration.");
        }

        return apiKey;
    }

    private static bool IsTtsModel(JsonNode? modelNode)
    {
        var modelId = modelNode?["id"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return false;
        }

        var outputModalities = modelNode?["architecture"]?["output_modalities"]?.AsArray();
        return outputModalities?.Any(modality =>
            string.Equals(modality?.GetValue<string>(), "speech", StringComparison.OrdinalIgnoreCase)) == true;
    }

    private static IEnumerable<TtsVoiceDto> GetVoices(JsonNode? modelNode)
    {
        var supportedVoices = modelNode?["supported_voices"]?.AsArray()
            .Select(voice => voice?.GetValue<string>())
            .Where(voice => !string.IsNullOrWhiteSpace(voice))
            .Cast<string>()
            .ToList();

        // Missing metadata means voices are unknown, not that OpenAI voice names work.
        var voices = (supportedVoices ?? []).Select(voice => (Id: voice, Name: voice));

        return voices.Select(voice => new TtsVoiceDto
        {
            VoiceId = voice.Id,
            Name = voice.Name,
            Language = WritingLanguage.English
        }).ToList();
    }

    private static string? GetHeader(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(", ", values) : null;

    private static string? GetErrorMessage(string errorResponse)
    {
        if (string.IsNullOrWhiteSpace(errorResponse)) return null;
        try
        {
            var root = JsonNode.Parse(errorResponse) as JsonObject;
            var error = root?["error"] as JsonObject;
            var message = ReadString(error?["message"]) ?? ReadString(root?["message"]);
            var metadata = error?["metadata"] as JsonObject;
            var raw = metadata?["raw"];
            var details = raw as JsonObject;
            var rawText = ReadString(raw);
            if (details is null && rawText is not null)
            {
                try { details = JsonNode.Parse(rawText) as JsonObject; }
                catch (JsonException) { /* Some providers return plain text errors. */ }
            }
            var detail = ReadString((details?["error"] as JsonObject)?["message"])
                         ?? ReadString(details?["message"]) ?? ReadString(details?["detail"]) ?? rawText;
            if (!string.IsNullOrWhiteSpace(detail) && detail != message)
                return $"{message ?? "OpenRouter TTS failed"}: {detail}";
            if (message?.StartsWith("Provider returned", StringComparison.OrdinalIgnoreCase) == true)
                return $"{message}. OpenRouter returned no further provider explanation. "
                       + "Check that the voice ID belongs to the selected model; voice names such as Alloy are not universal. "
                       + "See backend logs for the model, voice and request IDs.";
            return message;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
