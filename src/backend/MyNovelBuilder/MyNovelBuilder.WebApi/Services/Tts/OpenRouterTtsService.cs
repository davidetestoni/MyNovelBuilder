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
using NAudio.Wave;

namespace MyNovelBuilder.WebApi.Services.Tts;

/// <summary>
/// Text-to-speech service for OpenRouter.
/// </summary>
[RegisterKeyedService(TtsProvider.OpenRouter, useHttpClient: true)]
public class OpenRouterTtsService : ITtsService
{
    private const int SampleRate = 24000;
    private const short ChannelCount = 1;
    private const short BitsPerSample = 16;

    // OpenRouter's TTS docs use the same normalized built-in voice IDs across models,
    // while the models API does not currently expose per-model voice metadata.
    private static readonly (string Id, string Name)[] BuiltInVoices =
    [
        ("alloy", "Alloy"),
        ("ash", "Ash"),
        ("ballad", "Ballad"),
        ("cedar", "Cedar"),
        ("coral", "Coral"),
        ("echo", "Echo"),
        ("fable", "Fable"),
        ("marin", "Marin"),
        ("nova", "Nova"),
        ("onyx", "Onyx"),
        ("sage", "Sage"),
        ("shimmer", "Shimmer"),
        ("verse", "Verse")
    ];

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
            request,
            HttpCompletionOption.ResponseContentRead,
            cancellationToken);

        var pcmBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        using var wavStream = new MemoryStream();
        await using (var writer = new WaveFileWriter(
                         wavStream,
                         new WaveFormat(SampleRate, BitsPerSample, ChannelCount)))
        {
            await writer.WriteAsync(pcmBytes, cancellationToken);
        }

        return wavStream.ToArray();
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

        var pcmStream = await response.Content.ReadAsStreamAsync(cancellationToken);

        return new PcmWavStreamingStream(
            sampleRate: SampleRate,
            channels: ChannelCount,
            bitsPerSample: BitsPerSample,
            producer: async (writeAsync, ct) =>
            {
                await using var stream = pcmStream;
                var buffer = new byte[16 * 1024];

                while (true)
                {
                    var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
                    if (read == 0)
                    {
                        break;
                    }

                    await writeAsync(buffer.AsMemory(0, read));
                }
            },
            ct: cancellationToken,
            onDispose: response.Dispose);
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
        response.Dispose();

        _logger.LogError(
            "OpenRouter TTS generation failed. Status code: {StatusCode}, Response: {Response}",
            response.StatusCode,
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
            response_format = "pcm"
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "audio/speech")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
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

        var voices = supportedVoices is { Count: > 0 }
            ? supportedVoices.Select(voice => (Id: voice, Name: voice))
            : BuiltInVoices;

        return voices.Select(voice => new TtsVoiceDto
        {
            VoiceId = voice.Id,
            Name = voice.Name,
            Language = WritingLanguage.English
        }).ToList();
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
}
