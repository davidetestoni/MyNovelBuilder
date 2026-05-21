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
/// Service for generating images using DeAPI.
/// </summary>
[RegisterKeyedService(ImageGenerationProvider.DeApi, useHttpClient: true)]
public class DeApiImageGenerationService : DeApiGenerationServiceBase, IImageGenerationService
{
    private const int DefaultImageSteps = 8;
    private const int DefaultEditSteps = 20;

    private readonly ILogger<DeApiImageGenerationService> _logger;

    /// <summary></summary>
    public DeApiImageGenerationService(
        ILogger<DeApiImageGenerationService> logger,
        HttpClient httpClient,
        IIntegrationsService integrationsService)
        : base(httpClient, integrationsService)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<byte[]> GenerateImageAsync(
        ImageGenerationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var apiKey = await GetDeApiApiKeyAsync(cancellationToken);

        var httpRequest = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri(HttpClient.BaseAddress!, "txt2img"),
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    prompt = request.Prompt,
                    model = request.ModelId,
                    width = request.Width,
                    height = request.Height,
                    steps = DefaultImageSteps,
                    seed = Random.Shared.NextInt64()
                }), Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
        
        using var response = await HttpClient.SendAsync(httpRequest, cancellationToken);
        var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("DeAPI image generation failed. Status code: {StatusCode}, Response: {Response}",
                response.StatusCode, jsonResponse);
            throw new ApiException(ErrorCodes.ExternalServiceError,
                "Image generation failed with DeAPI.");
        }

        var responseObject = JsonNode.Parse(jsonResponse)!;
        var requestId = responseObject["data"]!["request_id"]!.GetValue<string>();

        return await PollForResultAsync(requestId, apiKey, "image generation", _logger, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<byte[]> EditImageAsync(
        byte[] imageBytes,
        ImageGenerationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var apiKey = await GetDeApiApiKeyAsync(cancellationToken);

        var content = new MultipartFormDataContent();
        content.Add(new StringContent(request.Prompt), "prompt");
        content.Add(new StringContent(request.ModelId), "model");
        content.Add(new StringContent(request.Width.ToString()), "width");
        content.Add(new StringContent(request.Height.ToString()), "height");
        content.Add(new StringContent(DefaultEditSteps.ToString()), "steps");
        content.Add(new StringContent(Random.Shared.NextInt64().ToString()), "seed");
        content.Add(CreateImageByteArrayContent(imageBytes), "image", "input.png");
        
        var httpRequest = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri(HttpClient.BaseAddress!, "img2img"),
            Content = content
        };
        httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
        
        using var response = await HttpClient.SendAsync(httpRequest, cancellationToken);
        var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("DeAPI image generation failed. Status code: {StatusCode}, Response: {Response}",
                response.StatusCode, jsonResponse);
            throw new ApiException(ErrorCodes.ExternalServiceError,
                "Image generation failed with DeAPI.");
        }

        var responseObject = JsonNode.Parse(jsonResponse)!;
        var requestId = responseObject["data"]!["request_id"]!.GetValue<string>();

        return await PollForResultAsync(requestId, apiKey, "image editing", _logger, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ImageGenerationModelInfo>> GetAvailableModelsAsync(
        CancellationToken cancellationToken = default)
    {
        var apiKey = await GetDeApiApiKeyAsync(cancellationToken);
        
        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(HttpClient.BaseAddress!, "models")
        };
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
        
        using var response = await HttpClient.SendAsync(request, cancellationToken);
        var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
        
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

            var supportsImageGeneration = inferenceTypes.Contains("txt2img");
            var supportsImageEditing = inferenceTypes.Contains("img2img");
            if (!supportsImageGeneration && !supportsImageEditing)
            {
                continue;
            }

            models.Add(new ImageGenerationModelInfo
            {
                ModelId = m["slug"]!.GetValue<string>(),
                Name = m["name"]!.GetValue<string>(),
                SupportsImageGeneration = supportsImageGeneration,
                SupportsImageEditing = supportsImageEditing,
            });
        }

        return models
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.ModelId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
