using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MyNovelBuilder.WebApi.Attributes;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Models.ImageGeneration;

namespace MyNovelBuilder.WebApi.Services.ImageGeneration;

/// <summary>
/// Service for generating images using NanoGPT.
/// </summary>
[RegisterKeyedService(ImageGenerationProvider.NanoGpt, useHttpClient: true)]
public class NanoGptImageGenerationService : IImageGenerationService
{
    private static readonly Uri _imageGenerationUri = new("https://nano-gpt.com/v1/images/generations");

    private readonly HttpClient _httpClient;
    private readonly ILogger<NanoGptImageGenerationService> _logger;
    private readonly IIntegrationsService _integrationsService;

    /// <summary></summary>
    public NanoGptImageGenerationService(
        HttpClient httpClient,
        ILogger<NanoGptImageGenerationService> logger,
        IIntegrationsService integrationsService)
    {
        _httpClient = httpClient;
        _logger = logger;
        _integrationsService = integrationsService;
        _httpClient.BaseAddress = new Uri("https://nano-gpt.com/api/");
        _httpClient.Timeout = TimeSpan.FromMinutes(3);
    }
    
    /// <inheritdoc />
    public async Task<byte[]> GenerateImageAsync(
        ImageGenerationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var apiKey = await GetApiKeyAsync(cancellationToken);
        var payload = new
        {
            model = request.ModelId,
            prompt = request.Prompt,
            n = 1,
            size = $"{request.Width}x{request.Height}",
            response_format = "url",
        };

        return await GenerateImageInternalAsync(payload, request.ModelId, apiKey, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<byte[]> EditImageAsync(
        byte[] imageBytes,
        ImageGenerationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var apiKey = await GetApiKeyAsync(cancellationToken);
        var payload = new
        {
            model = request.ModelId,
            prompt = request.Prompt,
            n = 1,
            size = $"{request.Width}x{request.Height}",
            response_format = "url",
            imageDataUrl = CreateImageDataUrl(imageBytes),
        };

        return await GenerateImageInternalAsync(payload, request.ModelId, apiKey, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ImageGenerationModelInfo>> GetAvailableModelsAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync("v1/image-models?detailed=true", cancellationToken);
        var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "NanoGPT image models request failed. Status code: {StatusCode}, Response: {Response}",
                response.StatusCode,
                jsonResponse);

            throw new ApiException(
                ErrorCodes.ExternalServiceError,
                GetErrorMessage(jsonResponse) ?? "Failed to get image generation models from NanoGPT.");
        }

        var responseObject = JsonNode.Parse(jsonResponse)?.AsObject()
                             ?? throw new ApiException(
                                 ErrorCodes.ExternalServiceError,
                                 "NanoGPT returned an invalid JSON response for image models.");

        var modelNodes = responseObject["data"]?.AsArray()
                         ?? throw new ApiException(
                             ErrorCodes.ExternalServiceError,
                             "NanoGPT image models response did not include a data array.");

        var models = new List<ImageGenerationModelInfo>();

        foreach (var modelNode in modelNodes)
        {
            if (modelNode is not JsonObject modelObject)
            {
                continue;
            }

            var modelId = modelObject["id"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(modelId))
            {
                continue;
            }

            var modelName = modelObject["name"]?.GetValue<string>() ?? modelId;
            var capabilities = modelObject["capabilities"];

            if (capabilities?["image_generation"]?.GetValue<bool>() == true)
            {
                models.Add(new ImageGenerationModelInfo
                {
                    ModelId = modelId,
                    Name = modelName,
                    IsImageEditor = false,
                });
            }

            if (capabilities?["image_to_image"]?.GetValue<bool>() == true)
            {
                models.Add(new ImageGenerationModelInfo
                {
                    ModelId = modelId,
                    Name = modelName,
                    IsImageEditor = true,
                });
            }
        }

        return models
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.ModelId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.IsImageEditor);
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

    private async Task<byte[]> GenerateImageInternalAsync<TPayload>(
        TPayload payload,
        string model,
        string apiKey,
        CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _imageGenerationUri)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
        httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "NanoGPT image generation failed. Status code: {StatusCode}, Response: {Response}",
                response.StatusCode,
                jsonResponse);

            throw new ApiException(
                ErrorCodes.ExternalServiceError,
                GetErrorMessage(jsonResponse) ?? "Image generation failed with NanoGPT.");
        }

        var responseObject = JsonNode.Parse(jsonResponse)?.AsObject()
                             ?? throw new ApiException(
                                 ErrorCodes.ExternalServiceError,
                                 "NanoGPT returned an invalid JSON response for image generation.");
        var cost = responseObject["cost"]?.GetValue<decimal?>();
        var paymentSource = responseObject["paymentSource"]?.GetValue<string>();

        LogCost(model, cost, paymentSource);

        var imageData = responseObject["data"]?.AsArray().FirstOrDefault()?.AsObject()
                        ?? throw new ApiException(
                            ErrorCodes.ExternalServiceError,
                            "NanoGPT image generation response did not include any image data.");

        var imageUrl = imageData["url"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            return await _httpClient.GetByteArrayAsync(imageUrl, cancellationToken);
        }

        var imageBase64 = imageData["b64_json"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(imageBase64))
        {
            try
            {
                return Convert.FromBase64String(imageBase64);
            }
            catch (FormatException ex)
            {
                _logger.LogError(ex, "NanoGPT image generation returned invalid base64 image data.");
                throw new ApiException(
                    ErrorCodes.ExternalServiceError,
                    "NanoGPT returned invalid image data.");
            }
        }

        throw new ApiException(
            ErrorCodes.ExternalServiceError,
            "NanoGPT image generation response did not contain a downloadable URL or base64 image.");
    }

    private static string CreateImageDataUrl(byte[] imageBytes)
    {
        var base64 = Convert.ToBase64String(imageBytes);
        return $"data:image/png;base64,{base64}";
    }

    private void LogCost(string model, decimal? cost, string? paymentSource)
    {
        if (!cost.HasValue)
        {
            return;
        }

        _logger.LogInformation(
            "NanoGPT image generation cost for model {Model}: {Cost} {PaymentSource}",
            model,
            cost.Value,
            paymentSource ?? "unknown");
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
                   ?? errorObject?["message"]?.GetValue<string>()
                   ?? errorObject?["error"]?.GetValue<string>();
        }
        catch
        {
            return null;
        }
    }
}
