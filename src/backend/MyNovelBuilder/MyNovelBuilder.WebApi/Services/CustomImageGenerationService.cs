using System.Text;
using System.Text.Json;
using Mapster;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Models.ImageGen;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Custom service for generating images.
/// </summary>
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
        var payload = request.Adapt<ImageGenRequest>();
        var jsonPayload = JsonSerializer.Serialize(payload, _jsonSerializerOptions);
        using var response = await _httpClient.PostAsync("generate/image",
            new StringContent(jsonPayload, Encoding.UTF8, "application/json"));
        
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadAsByteArrayAsync();
    }
}
