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
public class DeApiService : IImageGenerationService
{
    private readonly ILogger<DeApiService> _logger;
    private readonly HttpClient _httpClient;
    private readonly IIntegrationsService _integrationsService;

    /// <summary></summary>
    public DeApiService(
        ILogger<DeApiService> logger,
        HttpClient httpClient,
        IIntegrationsService integrationsService)
    {
        _logger = logger;
        _httpClient = httpClient;
        _integrationsService = integrationsService;
        _httpClient.BaseAddress = new Uri("https://api.deapi.ai/api/v1/client/");
    }
    
    /// <inheritdoc />
    public async Task<byte[]> GenerateImageAsync(ImageGenerationRequestDto request)
    {
        var config = await _integrationsService.GetConfigAsync();
        var apiKey = config.DeApiApiKey;
        
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ApiException(ErrorCodes.MissingOrInvalidServiceCredentials,
                "DeAPI API key is not configured.");
        }

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
        var timeout = TimeSpan.FromMinutes(2);
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
    public async Task<byte[]> EditImageAsync(byte[] imageBytes, ImageGenerationRequestDto request)
    {
        var config = await _integrationsService.GetConfigAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ImageGenerationModelInfo>> GetAvailableModelsAsync()
    {
        var config = await _integrationsService.GetConfigAsync();
        var apiKey = config.DeApiApiKey;
        
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ApiException(ErrorCodes.MissingOrInvalidServiceCredentials,
                "DeAPI API key is not configured.");
        }
        
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
        return responseObject["data"]!.AsArray()
            .Where(m => m!["inference_types"]!.AsArray().Select(t => t!.GetValue<string>()).Contains("txt2img"))
            .Select(m => new ImageGenerationModelInfo
            {
                ModelId = m!["slug"]!.GetValue<string>(),
                Name = m["name"]!.GetValue<string>()
            })
            .ToList();
    }
}
