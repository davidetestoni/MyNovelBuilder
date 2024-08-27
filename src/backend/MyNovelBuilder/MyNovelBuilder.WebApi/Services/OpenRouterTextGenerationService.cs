using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MyNovelBuilder.WebApi.Dtos.Prompt;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Models.OpenRouter;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for generating text using OpenRouter's OpenAI-compatible API.
/// </summary>
public class OpenRouterTextGenerationService : ITextGenerationService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    /// <summary></summary>
    public OpenRouterTextGenerationService(
        IConfiguration configuration,
        HttpClient httpClient)
    {
        _httpClient = httpClient;
        
        var apiKey = configuration["Secrets:OpenRouterApiKey"];
        
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ApiException(ErrorCodes.MissingOrInvalidServiceCredentials,
                "OpenRouter API key is missing.");
        }
        
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        _httpClient.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
        
        _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
        _jsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    }
    
    /// <inheritdoc />
    public async IAsyncEnumerable<string> GenerateStreamedAsync(IEnumerable<PromptMessageDto> messages)
    {
        // Hardcoded model for testing purposes
        var model = "undi95/toppy-m-7b:free";

        var payload = new ChatCompletionRequest
        {
            Model = model,
            Messages = messages.Select(message => new ChatCompletionMessage
            {
                Role = message.Role switch
                {
                    PromptMessageRole.System => ChatCompletionMessageRole.System,
                    PromptMessageRole.User => ChatCompletionMessageRole.User,
                    PromptMessageRole.Assistant => ChatCompletionMessageRole.Assistant,
                    _ => throw new ApiException(ErrorCodes.InternalServerError,
                        $"Invalid enum value: {message.Role}")
                },
                Content = message.Message
            })
        };
        
        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        request.RequestUri = new Uri(_httpClient.BaseAddress!, "chat/completions");
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload, _jsonSerializerOptions), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        // Stream the response content asynchronously
        await using var responseStream = await response.Content.ReadAsStreamAsync();

        // Deserialize asynchronously as the data is streamed
        using var streamReader = new StreamReader(responseStream);
        
        // The response is a newline-delimited JSON stream
        while (!streamReader.EndOfStream)
        {
            var line = await streamReader.ReadLineAsync();

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            
            // TODO: Deserialize the JSON line
            yield return line!;
        }
    }
}
