using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using MyNovelBuilder.WebApi.Dtos.Prompt;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;
using OpenAI;
using OpenAI.Chat;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for generating text using OpenRouter's OpenAI-compatible API.
/// </summary>
public class OpenRouterTextGenerationService : ITextGenerationService
{
    private readonly OpenAIClient _openAiClient;

    /// <summary></summary>
    public OpenRouterTextGenerationService(
        IConfiguration configuration)
    {
        var apiKey = configuration["Secrets:OpenRouterApiKey"];
        
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ApiException(ErrorCodes.MissingOrInvalidServiceCredentials,
                "OpenRouter API key is missing.");
        }
        
        _openAiClient = new OpenAIClient(apiKey, new OpenAIClientOptions
        {
            Endpoint = new Uri("https://openrouter.ai/api")
        });
    }
    
    /// <inheritdoc />
    public async IAsyncEnumerable<string> GenerateStreamedAsync(
        string model,
        IEnumerable<PromptMessageDto> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var chatClient = _openAiClient.GetChatClient(model);
        
        var chatMessages = messages.Select(ToChatMessage).ToList();

        await foreach (var update in chatClient.CompleteChatStreamingAsync(chatMessages, cancellationToken: cancellationToken))
        {
            foreach (var message in update.ContentUpdate)
            {
                if (!string.IsNullOrWhiteSpace(message.Refusal))
                {
                    throw new ApiException(ErrorCodes.ExternalServiceError, 
                        $"OpenRouter refused to generate text: {message.Refusal}");
                }
                
                yield return message.Text;
            }
        }
    }

    private static ChatMessage ToChatMessage(PromptMessageDto message) =>
        message.Role switch
        {
            PromptMessageRole.User => new UserChatMessage(message.Message),
            PromptMessageRole.System => new SystemChatMessage(message.Message),
            PromptMessageRole.Assistant => new AssistantChatMessage(message.Message),
            _ => throw new NotSupportedException($"Unsupported message role: {message.Role}")
        };
}
