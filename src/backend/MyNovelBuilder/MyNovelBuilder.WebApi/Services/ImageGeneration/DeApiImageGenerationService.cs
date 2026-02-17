using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Models.ImageGeneration;

using MyNovelBuilder.WebApi.Attributes;
using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Services.ImageGeneration;

/// <summary>
/// Service for generating images using DeAPI.
/// </summary>
[RegisterKeyedService(ImageGenerationProvider.DeApi, useHttpClient: true)]
public class DeApiImageGenerationService : IImageGenerationService
{
    private readonly ILogger<DeApiImageGenerationService> _logger;
    private readonly HttpClient _httpClient;
    private readonly IIntegrationsService _integrationsService;

    /// <summary></summary>
    public DeApiImageGenerationService(
        ILogger<DeApiImageGenerationService> logger,
        HttpClient httpClient,
        IIntegrationsService integrationsService)
    {
        _logger = logger;
        _httpClient = httpClient;
        _integrationsService = integrationsService;
        _httpClient.BaseAddress = new Uri("https://api.deapi.ai/api/v1/client/");
    }

    private async Task<string> GetApiKeyAsync()
    {
        var config = await _integrationsService.GetConfigAsync();
        var apiKey = config.DeApiApiKey;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ApiException(ErrorCodes.MissingOrInvalidServiceCredentials,
                "DeAPI API key is not configured.");
        }
        
        return apiKey;
    }

    /// <inheritdoc />
    public async Task<byte[]> GenerateImageAsync(ImageGenerationRequestDto request)
    {
        var apiKey = await GetApiKeyAsync();

        var httpRequest = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri(_httpClient.BaseAddress!, "txt2img"),
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    prompt = request.Prompt,
                    model = request.ModelId,
                    width = request.Width,
                    height = request.Height,
                    steps = 8,
                    seed = Random.Shared.NextInt64()
                }), Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
        
        using var response = await _httpClient.SendAsync(httpRequest);
        var jsonResponse = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("DeAPI image generation failed. Status code: {StatusCode}, Response: {Response}",
                response.StatusCode, jsonResponse);
            throw new ApiException(ErrorCodes.ExternalServiceError,
                "Image generation failed with DeAPI.");
        }

        var responseObject = JsonNode.Parse(jsonResponse)!;
        var requestId = responseObject["data"]!["request_id"]!.GetValue<string>();

        return await PollForResultAsync(requestId, apiKey);
    }

    /// <inheritdoc />
    public async Task<byte[]> EditImageAsync(byte[] imageBytes, ImageGenerationRequestDto request)
    {
        var apiKey = await GetApiKeyAsync();

        var content = new MultipartFormDataContent();
        content.Add(new StringContent(request.Prompt), "prompt");
        content.Add(new StringContent(request.ModelId), "model");
        content.Add(new StringContent(request.Width.ToString()), "width");
        content.Add(new StringContent(request.Height.ToString()), "height");
        content.Add(new StringContent("20"), "steps");
        content.Add(new StringContent(Random.Shared.NextInt64().ToString()), "seed");
        content.Add(new ByteArrayContent(imageBytes), "image", "input.png");
        
        var httpRequest = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri(_httpClient.BaseAddress!, "img2img"),
            Content = content
        };
        httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
        
        using var response = await _httpClient.SendAsync(httpRequest);
        var jsonResponse = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("DeAPI image generation failed. Status code: {StatusCode}, Response: {Response}",
                response.StatusCode, jsonResponse);
            throw new ApiException(ErrorCodes.ExternalServiceError,
                "Image generation failed with DeAPI.");
        }

        var responseObject = JsonNode.Parse(jsonResponse)!;
        var requestId = responseObject["data"]!["request_id"]!.GetValue<string>();

        return await PollForResultAsync(requestId, apiKey);
    }

    private async Task<byte[]> PollForResultAsync(string requestId, string apiKey)
    {
        var timeout = TimeSpan.FromMinutes(5);
        var startTime = DateTime.UtcNow;

        // Polling
        while (DateTime.UtcNow - startTime < timeout)
        {
            await Task.Delay(2000);

            var statusRequest = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(_httpClient.BaseAddress!, $"request-status/{requestId}")
            };
            statusRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");

            using var statusResponse = await _httpClient.SendAsync(statusRequest);
            var statusJson = await statusResponse.Content.ReadAsStringAsync();

            if (!statusResponse.IsSuccessStatusCode)
            {
                _logger.LogError("DeAPI image generation status check failed. Status code: {StatusCode}, Response: {Response}",
                    statusResponse.StatusCode, statusJson);

                throw new ApiException(ErrorCodes.ExternalServiceError,
                    "Failed to check image generation status with DeAPI.");
            }

            var statusObject = JsonNode.Parse(statusJson)!;

            // status can be "pending", "processing", "done" or "error"
            var status = statusObject["data"]!["status"]!.GetValue<string>();

            if (status == "done")
            {
                var imageUrl = statusObject["data"]!["result_url"]!.GetValue<string>();
                return await _httpClient.GetByteArrayAsync(imageUrl);
            }

            if (status == "error")
            {
                _logger.LogError("DeAPI image generation failed. Request ID: {RequestId}, Status response: {StatusResponse}",
                    requestId, statusJson);

                throw new ApiException(ErrorCodes.ExternalServiceError,
                    "Image generation failed with DeAPI.");
            }
        }

        _logger.LogError("DeAPI image generation timed out. Request ID: {RequestId}", requestId);
        throw new ApiException(ErrorCodes.ExternalServiceError,
            "Image generation with DeAPI timed out.");
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ImageGenerationModelInfo>> GetAvailableModelsAsync()
    {
        var apiKey = await GetApiKeyAsync();
        
        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(_httpClient.BaseAddress!, "models")
        };
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
        
        using var response = await _httpClient.SendAsync(request);
        var jsonResponse = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("DeAPI get models failed. Status code: {StatusCode}, Response: {Response}",
                response.StatusCode, jsonResponse);
            throw new ApiException(ErrorCodes.ExternalServiceError,
                "Failed to get image generation models from DeAPI.");
        }
        
        var responseObject = JsonNode.Parse(jsonResponse)!;
        var models = new List<ImageGenerationModelInfo>();

        foreach (var m in responseObject["data"]!.AsArray())
        {
            var inferenceTypes = m!["inference_types"]?.AsArray().Select(t => t!.GetValue<string>()).ToList() ?? [];
            
            if (inferenceTypes.Contains("txt2img"))
            {
                models.Add(new ImageGenerationModelInfo
                {
                    ModelId = m["slug"]!.GetValue<string>(),
                    Name = m["name"]!.GetValue<string>(),
                    IsImageEditor = false
                });
            }
            
            if (inferenceTypes.Contains("img2img"))
            {
                models.Add(new ImageGenerationModelInfo
                {
                    ModelId = m["slug"]!.GetValue<string>(),
                    Name = m["name"]!.GetValue<string>(),
                    IsImageEditor = true
                });
            }
        }
        
        return models;
    }
}
