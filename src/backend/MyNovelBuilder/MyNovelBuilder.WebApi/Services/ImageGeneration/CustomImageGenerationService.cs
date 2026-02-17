using System.Text;
using System.Text.Json;
using Mapster;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Models.ImageGeneration;

using MyNovelBuilder.WebApi.Attributes;
using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Services.ImageGeneration;

/// <summary>
/// Custom service for generating images.
/// </summary>
[RegisterKeyedService(ImageGenerationProvider.Custom, useHttpClient: true)]
public class CustomImageGenerationService : IImageGenerationService
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    
    /// <summary></summary>
    public CustomImageGenerationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("http://localhost:5000");
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
    }
    
    /// <inheritdoc />
    public async Task<byte[]> GenerateImageAsync(ImageGenRequestDto request)
    {
        var payload = request.Adapt<ImageGenerationRequest>();
        var jsonPayload = JsonSerializer.Serialize(payload, _jsonSerializerOptions);
        using var response = await _httpClient.PostAsync("generate/image",
            new StringContent(jsonPayload, Encoding.UTF8, "application/json"));
        
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadAsByteArrayAsync();
    }

    /// <inheritdoc />
    public Task<IEnumerable<ImageGenerationModelInfo>> GetAvailableModelsAsync()
    {
        return Task.FromResult((IEnumerable<ImageGenerationModelInfo>)[]);
    }
}
