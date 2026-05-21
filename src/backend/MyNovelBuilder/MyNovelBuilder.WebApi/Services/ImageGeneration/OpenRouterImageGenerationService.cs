using System.Net.Http.Headers;
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
/// Service for generating images using OpenRouter.
/// </summary>
[RegisterKeyedService(ImageGenerationProvider.OpenRouter, useHttpClient: true)]
public class OpenRouterImageGenerationService : IImageGenerationService
{
    private static readonly (string AspectRatio, double Value)[] SupportedAspectRatios =
    [
        ("1:8", 1d / 8d),
        ("1:4", 1d / 4d),
        ("9:16", 9d / 16d),
        ("2:3", 2d / 3d),
        ("3:4", 3d / 4d),
        ("4:5", 4d / 5d),
        ("1:1", 1d),
        ("5:4", 5d / 4d),
        ("4:3", 4d / 3d),
        ("3:2", 3d / 2d),
        ("16:9", 16d / 9d),
        ("4:1", 4d),
        ("8:1", 8d),
        ("21:9", 21d / 9d)
    ];

    private const double AspectRatioTolerance = 0.05;

    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenRouterImageGenerationService> _logger;
    private readonly IIntegrationsService _integrationsService;

    /// <summary></summary>
    public OpenRouterImageGenerationService(
        HttpClient httpClient,
        ILogger<OpenRouterImageGenerationService> logger,
        IIntegrationsService integrationsService)
    {
        _httpClient = httpClient;
        _logger = logger;
        _integrationsService = integrationsService;
        _httpClient.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
    }

    /// <inheritdoc />
    public Task<byte[]> GenerateImageAsync(
        ImageGenerationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        return GenerateImageInternalAsync(request, imageBytes: null, cancellationToken);
    }

    /// <inheritdoc />
    public Task<byte[]> EditImageAsync(
        byte[] imageBytes,
        ImageGenerationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        return GenerateImageInternalAsync(request, imageBytes, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ImageGenerationModelInfo>> GetAvailableModelsAsync(
        CancellationToken cancellationToken = default)
    {
        var apiKey = await GetApiKeyAsync(cancellationToken);
        var models = await GetAvailableModelMetadataAsync(apiKey, cancellationToken);

        return models
            .Select(model => new ImageGenerationModelInfo
            {
                ModelId = model.ModelId,
                Name = model.Name,
                SupportsImageGeneration = true,
                SupportsImageEditing = model.SupportsImageInput,
            })
            .OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(model => model.ModelId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<byte[]> GenerateImageInternalAsync(
        ImageGenerationRequestDto request,
        byte[]? imageBytes,
        CancellationToken cancellationToken)
    {
        var apiKey = await GetApiKeyAsync(cancellationToken);
        var model = await GetModelMetadataAsync(request.ModelId, apiKey, cancellationToken);

        if (imageBytes is not null && !model.SupportsImageInput)
        {
            throw new ApiException(
                ErrorCodes.BadRequest,
                $"OpenRouter model '{request.ModelId}' does not support image editing.");
        }

        using var httpRequest = CreateImageGenerationRequest(
            request,
            model,
            apiKey,
            imageBytes);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "OpenRouter image generation failed. Status code: {StatusCode}, Response: {Response}",
                response.StatusCode,
                jsonResponse);

            throw new ApiException(
                ErrorCodes.ExternalServiceError,
                GetErrorMessage(jsonResponse) ?? "Image generation failed with OpenRouter.");
        }

        var root = JsonNode.Parse(jsonResponse)?.AsObject()
                   ?? throw new ApiException(
                       ErrorCodes.ExternalServiceError,
                       "OpenRouter returned an invalid JSON response for image generation.");

        var imageUrl = root["choices"]?.AsArray()
            .FirstOrDefault()?["message"]?["images"]?.AsArray()
            .FirstOrDefault()?["image_url"]?["url"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            throw new ApiException(
                ErrorCodes.ExternalServiceError,
                "OpenRouter image generation response did not include any image data.");
        }

        return await ReadImageBytesAsync(imageUrl, cancellationToken);
    }

    private HttpRequestMessage CreateImageGenerationRequest(
        ImageGenerationRequestDto request,
        OpenRouterImageModelMetadata model,
        string apiKey,
        byte[]? imageBytes)
    {
        object content = imageBytes is null
            ? request.Prompt
            : new object[]
            {
                new
                {
                    type = "text",
                    text = request.Prompt
                },
                new
                {
                    type = "image_url",
                    image_url = new
                    {
                        url = CreateImageDataUrl(imageBytes)
                    }
                }
            };

        object[] messages =
        [
            new
            {
                role = "user",
                content
            }
        ];

        var payload = new Dictionary<string, object?>
        {
            ["model"] = request.ModelId,
            ["messages"] = messages,
            ["modalities"] = model.SupportsTextOutput ? new[] { "image", "text" } : new[] { "image" },
            ["stream"] = false
        };

        var imageConfig = CreateImageConfig(request.Width, request.Height);
        if (imageConfig is not null)
        {
            payload["image_config"] = imageConfig;
        }

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return httpRequest;
    }

    private async Task<IReadOnlyList<OpenRouterImageModelMetadata>> GetAvailableModelMetadataAsync(
        string apiKey,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "models?output_modalities=image");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "OpenRouter image models request failed. Status code: {StatusCode}, Response: {Response}",
                response.StatusCode,
                jsonResponse);

            throw new ApiException(
                ErrorCodes.ExternalServiceError,
                GetErrorMessage(jsonResponse) ?? "Failed to get image generation models from OpenRouter.");
        }

        var root = JsonNode.Parse(jsonResponse)?.AsObject()
                   ?? throw new ApiException(
                       ErrorCodes.ExternalServiceError,
                       "OpenRouter returned an invalid JSON response for image models.");

        var modelNodes = root["data"]?.AsArray()
                         ?? throw new ApiException(
                             ErrorCodes.ExternalServiceError,
                             "OpenRouter image models response did not include a data array.");

        return modelNodes
            .Select(ToModelMetadata)
            .Where(model => model is not null)
            .Select(model => model!)
            .ToList();
    }

    private async Task<OpenRouterImageModelMetadata> GetModelMetadataAsync(
        string modelId,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var model = (await GetAvailableModelMetadataAsync(apiKey, cancellationToken))
            .FirstOrDefault(model => string.Equals(
                model.ModelId,
                modelId,
                StringComparison.OrdinalIgnoreCase));

        if (model is null)
        {
            throw new ApiException(
                ErrorCodes.BadRequest,
                $"OpenRouter image model '{modelId}' is not available.");
        }

        return model;
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

    private async Task<byte[]> ReadImageBytesAsync(
        string imageUrl,
        CancellationToken cancellationToken)
    {
        if (imageUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var commaIndex = imageUrl.IndexOf(',');
            if (commaIndex < 0 || !imageUrl[..commaIndex].Contains(";base64", StringComparison.OrdinalIgnoreCase))
            {
                throw new ApiException(
                    ErrorCodes.ExternalServiceError,
                    "OpenRouter returned an unsupported image data URL.");
            }

            try
            {
                return Convert.FromBase64String(imageUrl[(commaIndex + 1)..]);
            }
            catch (FormatException ex)
            {
                _logger.LogError(ex, "OpenRouter image generation returned invalid base64 image data.");
                throw new ApiException(
                    ErrorCodes.ExternalServiceError,
                    "OpenRouter returned invalid image data.");
            }
        }

        return await _httpClient.GetByteArrayAsync(imageUrl, cancellationToken);
    }

    private static OpenRouterImageModelMetadata? ToModelMetadata(JsonNode? modelNode)
    {
        var modelId = modelNode?["id"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return null;
        }

        var name = modelNode?["name"]?.GetValue<string>() ?? modelId;
        var inputModalities = modelNode?["architecture"]?["input_modalities"]?.AsArray();
        var outputModalities = modelNode?["architecture"]?["output_modalities"]?.AsArray();

        var supportsImageOutput = outputModalities?.Any(modality =>
            string.Equals(modality?.GetValue<string>(), "image", StringComparison.OrdinalIgnoreCase)) == true;

        if (!supportsImageOutput)
        {
            return null;
        }

        var supportsImageInput = inputModalities?.Any(modality =>
            string.Equals(modality?.GetValue<string>(), "image", StringComparison.OrdinalIgnoreCase)) == true;
        var supportsTextOutput = outputModalities?.Any(modality =>
            string.Equals(modality?.GetValue<string>(), "text", StringComparison.OrdinalIgnoreCase)) == true;

        return new OpenRouterImageModelMetadata(
            modelId,
            name,
            supportsImageInput,
            supportsTextOutput);
    }

    private static object? CreateImageConfig(int width, int height)
    {
        var aspectRatio = ResolveAspectRatio(width, height);
        return aspectRatio is null
            ? null
            : new
            {
                aspect_ratio = aspectRatio
            };
    }

    private static string? ResolveAspectRatio(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        var requestedRatio = (double)width / height;
        var bestMatch = SupportedAspectRatios
            .Select(ratio => new
            {
                ratio.AspectRatio,
                Delta = Math.Abs(ratio.Value - requestedRatio)
            })
            .OrderBy(match => match.Delta)
            .First();

        return bestMatch.Delta <= AspectRatioTolerance
            ? bestMatch.AspectRatio
            : null;
    }

    private static string CreateImageDataUrl(byte[] imageBytes)
    {
        var mimeType = DetectImageMimeType(imageBytes);
        return $"data:{mimeType};base64,{Convert.ToBase64String(imageBytes)}";
    }

    private static string DetectImageMimeType(byte[] imageBytes)
    {
        if (imageBytes.Length >= 8
            && imageBytes[0] == 0x89
            && imageBytes[1] == 0x50
            && imageBytes[2] == 0x4E
            && imageBytes[3] == 0x47
            && imageBytes[4] == 0x0D
            && imageBytes[5] == 0x0A
            && imageBytes[6] == 0x1A
            && imageBytes[7] == 0x0A)
        {
            return "image/png";
        }

        if (imageBytes.Length >= 3
            && imageBytes[0] == 0xFF
            && imageBytes[1] == 0xD8
            && imageBytes[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (imageBytes.Length >= 12
            && imageBytes[0] == 0x52
            && imageBytes[1] == 0x49
            && imageBytes[2] == 0x46
            && imageBytes[3] == 0x46
            && imageBytes[8] == 0x57
            && imageBytes[9] == 0x45
            && imageBytes[10] == 0x42
            && imageBytes[11] == 0x50)
        {
            return "image/webp";
        }

        if (imageBytes.Length >= 6)
        {
            var header = Encoding.ASCII.GetString(imageBytes, 0, 6);
            if (header is "GIF87a" or "GIF89a")
            {
                return "image/gif";
            }
        }

        return "image/png";
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

    private sealed record OpenRouterImageModelMetadata(
        string ModelId,
        string Name,
        bool SupportsImageInput,
        bool SupportsTextOutput);
}
