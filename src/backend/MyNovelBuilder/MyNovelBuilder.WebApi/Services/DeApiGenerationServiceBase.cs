using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using MyNovelBuilder.WebApi.Exceptions;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Shared helpers for DeAPI-backed media generation services.
/// </summary>
public abstract class DeApiGenerationServiceBase
{
    private readonly HttpClient _httpClient;
    private readonly IIntegrationsService _integrationsService;

    /// <summary></summary>
    protected DeApiGenerationServiceBase(
        HttpClient httpClient,
        IIntegrationsService integrationsService)
    {
        _httpClient = httpClient;
        _integrationsService = integrationsService;
        _httpClient.BaseAddress = new Uri("https://api.deapi.ai/api/v1/client/");
    }

    /// <summary></summary>
    protected HttpClient HttpClient => _httpClient;

    /// <summary>
    /// Resolve the configured DeAPI API key.
    /// </summary>
    protected async Task<string> GetDeApiApiKeyAsync(CancellationToken cancellationToken = default)
    {
        var config = await _integrationsService.GetConfigAsync(cancellationToken);
        var apiKey = config.DeApiApiKey;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ApiException(
                ErrorCodes.MissingOrInvalidServiceCredentials,
                "DeAPI API key is not configured.");
        }

        return apiKey;
    }

    /// <summary>
    /// Poll DeAPI until the request completes and return the downloaded asset bytes.
    /// </summary>
    protected async Task<byte[]> PollForResultAsync(
        string requestId,
        string apiKey,
        string operation,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var timeout = TimeSpan.FromMinutes(5);
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < timeout)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);

            var statusRequest = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(_httpClient.BaseAddress!, $"request-status/{requestId}")
            };
            statusRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");

            using var statusResponse = await _httpClient.SendAsync(statusRequest, cancellationToken);
            var statusJson = await statusResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!statusResponse.IsSuccessStatusCode)
            {
                logger.LogError(
                    "DeAPI {Operation} status check failed. Status code: {StatusCode}, Response: {Response}",
                    operation,
                    statusResponse.StatusCode,
                    statusJson);

                throw new ApiException(
                    ErrorCodes.ExternalServiceError,
                    $"Failed to check {operation} status with DeAPI.");
            }

            var statusObject = JsonNode.Parse(statusJson)!;
            var status = statusObject["data"]!["status"]!.GetValue<string>();

            if (status == "done")
            {
                var resultUrl = statusObject["data"]!["result_url"]!.GetValue<string>();
                return await _httpClient.GetByteArrayAsync(resultUrl, cancellationToken);
            }

            if (status == "error")
            {
                logger.LogError(
                    "DeAPI {Operation} failed. Request ID: {RequestId}, Status response: {StatusResponse}",
                    operation,
                    requestId,
                    statusJson);

                throw new ApiException(
                    ErrorCodes.ExternalServiceError,
                    $"{operation[..1].ToUpperInvariant()}{operation[1..]} failed with DeAPI.");
            }
        }

        logger.LogError("DeAPI {Operation} timed out. Request ID: {RequestId}", operation, requestId);
        throw new ApiException(
            ErrorCodes.ExternalServiceError,
            $"{operation[..1].ToUpperInvariant()}{operation[1..]} with DeAPI timed out.");
    }

    /// <summary>
    /// Wrap image bytes as multipart content with the best detected MIME type.
    /// </summary>
    protected static ByteArrayContent CreateImageByteArrayContent(byte[] imageBytes)
    {
        var imageContent = new ByteArrayContent(imageBytes);
        imageContent.Headers.ContentType =
            MediaTypeHeaderValue.Parse(DetectImageMimeType(imageBytes));
        return imageContent;
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
}
