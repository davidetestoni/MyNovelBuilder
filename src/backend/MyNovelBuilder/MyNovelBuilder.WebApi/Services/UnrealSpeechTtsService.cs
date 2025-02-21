using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Exceptions;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Text-to-speech service from unrealspeech.com.
/// </summary>
public class UnrealSpeechTtsService : ITtsService
{
    private readonly HttpClient _httpClient;

    private readonly string[] _voices =
    [
        // American Female
        "Autumn", "Melody", "Hannah", "Emily", "Ivy", "Kaitlyn", "Luna", "Willow", "Lauren", "Sierra",
    
        // American Male
        "Noah", "Jasper", "Caleb", "Ronan", "Ethan", "Daniel", "Zane",
    
        // Chinese Female
        "Mei", "Lian", "Ting", "Jing",
    
        // Chinese Male
        "Wei", "Jian", "Hao", "Sheng",
    
        // Spanish Female
        "Lucía",
    
        // Spanish Male
        "Mateo", "Javier",
    
        // French Female
        "Élodie",
    
        // Hindi Female
        "Ananya", "Priya",
    
        // Hindi Male
        "Arjun", "Rohan",
    
        // Italian Female
        "Giulia",
    
        // Italian Male
        "Luca",
    
        // Portuguese Female
        "Camila",
    
        // Portuguese Male
        "Thiago", "Rafael"
    ];

    /// <summary></summary>
    public UnrealSpeechTtsService(IConfiguration configuration, HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("https://api.v8.unrealspeech.com");
        
        var apiKey = configuration["Secrets:UnrealSpeechApiKey"];
        
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ApiException(ErrorCodes.MissingOrInvalidServiceCredentials,
                "UnrealSpeech API key is missing.");
        }
        
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }
    
    /// <inheritdoc />
    public async Task<byte[]> GenerateAudioAsync(TtsRequestDto request)
    {
        // Create the task
        var payload = new
        {
            Text = request.Message,
            VoiceId = request.VoiceId,
            Bitrate = "320k",
            AudioFormat = "mp3",
            OutputFormat = "uri",
            TimestampType = "sentence",
            sync = true
        };
        var jsonPayload = JsonSerializer.Serialize(payload);
        using var response = await _httpClient.PostAsync("synthesisTasks",
            new StringContent(jsonPayload, Encoding.UTF8, "application/json"));
        
        var jsonResponse = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
        {
            throw new ApiException(ErrorCodes.ExternalServiceError,
                $"UnrealSpeech refused to generate audio: {jsonResponse}");
        }

        var responseObject = JsonNode.Parse(jsonResponse)!;
        
        var outputUri = responseObject["SynthesisTask"]!["OutputUri"]!.ToString();

        // Wait for the audio to be generated and uploaded to S3
        // This service is pretty unreliable...
        await Task.Delay(15000);
        
        // Read the audio from the output uri
        // We use a brand new HttpClient here, otherwise it has the
        // Authorization header set and the request fails
        // (although we should be using the IHttpClientFactory to create it...)
        using var httpClient = new HttpClient();
        using var audioResponse = await httpClient.GetAsync(outputUri);
        
        if (!audioResponse.IsSuccessStatusCode)
        {
            throw new ApiException(ErrorCodes.ExternalServiceError,
                $"Failed to fetch the generated audio file from the output uri: {await audioResponse.Content.ReadAsStringAsync()}");
        }
        
        return await audioResponse.Content.ReadAsByteArrayAsync();
    }

    /// <inheritdoc />
    public Task<IEnumerable<TtsVoiceDto>> GetVoicesAsync()
    {
        return Task.FromResult(_voices.Select(v => new TtsVoiceDto
        {
            VoiceId = v,
            Name = v,
            PreviewUrl = "https://example.com"
        }));
    }
}