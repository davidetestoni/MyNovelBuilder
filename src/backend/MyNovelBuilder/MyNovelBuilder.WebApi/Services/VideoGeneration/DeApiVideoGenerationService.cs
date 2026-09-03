using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MyNovelBuilder.WebApi.Attributes;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Models.VideoGeneration;

namespace MyNovelBuilder.WebApi.Services.VideoGeneration;

/// <summary>
/// Service for generating videos using DeAPI.
/// </summary>
[RegisterKeyedService(VideoGenerationProvider.DeApi, useHttpClient: true)]
public class DeApiVideoGenerationService : DeApiGenerationServiceBase, IVideoGenerationService
{
    private const double DefaultGuidance = 7.5;
    private const int DefaultVideoSteps = 20;
    private const int DefaultVideoFrames = 96;
    private const int DefaultVideoFps = 24;

    private readonly ILogger<DeApiVideoGenerationService> _logger;

    /// <summary></summary>
    public DeApiVideoGenerationService(
        ILogger<DeApiVideoGenerationService> logger,
        HttpClient httpClient,
        IIntegrationsService integrationsService)
        : base(httpClient, integrationsService)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<byte[]> GenerateVideoAsync(
        VideoGenerationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var apiKey = await GetDeApiApiKeyAsync(cancellationToken);
        var (width, height) = DeApiVideoRequestSizing.FitWithinLimits(request.Width, request.Height);

        var httpRequest = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri(HttpClient.BaseAddress!, "txt2video"),
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    prompt = request.Prompt,
                    model = request.ModelId,
                    width,
                    height,
                    guidance = DefaultGuidance,
                    steps = DefaultVideoSteps,
                    frames = DefaultVideoFrames,
                    fps = DefaultVideoFps,
                    seed = Random.Shared.NextInt64()
                }),
                Encoding.UTF8,
                "application/json")
        };
        httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");

        using var response = await HttpClient.SendAsync(httpRequest, cancellationToken);
        var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "DeAPI video generation failed. Status code: {StatusCode}, Response: {Response}",
                response.StatusCode,
                jsonResponse);
            throw new ApiException(
                ErrorCodes.ExternalServiceError,
                "Video generation failed with DeAPI.");
        }

        var responseObject = JsonNode.Parse(jsonResponse)!;
        var requestId = responseObject["data"]!["request_id"]!.GetValue<string>();

        return await PollForResultAsync(requestId, apiKey, "video generation", _logger, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<byte[]> GenerateVideoFromImageAsync(
        byte[] imageBytes,
        VideoGenerationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var apiKey = await GetDeApiApiKeyAsync(cancellationToken);
        var (width, height) = DeApiVideoRequestSizing.FitWithinLimits(request.Width, request.Height);

        var content = new MultipartFormDataContent();
        content.Add(new StringContent(request.Prompt), "prompt");
        content.Add(new StringContent(request.ModelId), "model");
        content.Add(new StringContent(width.ToString()), "width");
        content.Add(new StringContent(height.ToString()), "height");
        content.Add(new StringContent(DefaultGuidance.ToString(CultureInfo.InvariantCulture)), "guidance");
        content.Add(new StringContent(DefaultVideoSteps.ToString()), "steps");
        content.Add(new StringContent(DefaultVideoFrames.ToString()), "frames");
        content.Add(new StringContent(DefaultVideoFps.ToString()), "fps");
        content.Add(new StringContent(Random.Shared.NextInt64().ToString()), "seed");
        content.Add(CreateImageByteArrayContent(imageBytes), "first_frame_image", "first-frame.png");

        var httpRequest = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri(HttpClient.BaseAddress!, "img2video"),
            Content = content
        };
        httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");

        using var response = await HttpClient.SendAsync(httpRequest, cancellationToken);
        var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "DeAPI image-to-video generation failed. Status code: {StatusCode}, Response: {Response}",
                response.StatusCode,
                jsonResponse);
            throw new ApiException(
                ErrorCodes.ExternalServiceError,
                "Image-to-video generation failed with DeAPI.");
        }

        var responseObject = JsonNode.Parse(jsonResponse)!;
        var requestId = responseObject["data"]!["request_id"]!.GetValue<string>();

        return await PollForResultAsync(requestId, apiKey, "image-to-video generation", _logger, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<VideoGenerationModelInfo>> GetAvailableModelsAsync(
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
            _logger.LogError(
                "DeAPI get video models failed. Status code: {StatusCode}, Response: {Response}",
                response.StatusCode,
                jsonResponse);
            throw new ApiException(
                ErrorCodes.ExternalServiceError,
                "Failed to get video generation models from DeAPI.");
        }

        var responseObject = JsonNode.Parse(jsonResponse)!;
        var models = new List<VideoGenerationModelInfo>();

        foreach (var model in responseObject["data"]!.AsArray())
        {
            var inferenceTypes = model!["inference_types"]?.AsArray()
                .Select(type => type!.GetValue<string>())
                .ToList() ?? [];

            var supportsTextToVideo = inferenceTypes.Contains("txt2video");
            var supportsImageToVideo = inferenceTypes.Contains("img2video");

            if (!supportsTextToVideo && !supportsImageToVideo)
            {
                continue;
            }

            models.Add(new VideoGenerationModelInfo
            {
                ModelId = model["slug"]!.GetValue<string>(),
                Name = model["name"]!.GetValue<string>(),
                SupportsTextToVideo = supportsTextToVideo,
                SupportsImageToVideo = supportsImageToVideo,
            });
        }

        return models
            .OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(model => model.ModelId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
