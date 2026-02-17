using System.ClientModel;
using System.Runtime.CompilerServices;
using MyNovelBuilder.WebApi.Dtos.Prompt;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Models.TextGeneration;
using OpenAI;
using OpenAI.Chat;
using MyNovelBuilder.WebApi.Attributes;

namespace MyNovelBuilder.WebApi.Services.TextGeneration;

/// <summary>
/// Service for generating text using OpenRouter's OpenAI-compatible API.
/// </summary>
[RegisterKeyedService(TextGenerationProvider.OpenRouter)]
public class OpenRouterTextGenerationService : ITextGenerationService
{
    private readonly IIntegrationsService _integrationsService;
    private readonly OpenAIClient? _openAiClient = null;

    /// <summary></summary>
    public OpenRouterTextGenerationService(
        IIntegrationsService integrationsService)
    {
        _integrationsService = integrationsService;
    }
    
    private async ValueTask<OpenAIClient> GetOpenAiClientAsync()
    {
        if (_openAiClient is not null)
        {
            return _openAiClient;
        }

        var config = await _integrationsService.GetConfigAsync();

        if (string.IsNullOrWhiteSpace(config.OpenRouterApiKey))
        {
            throw new ApiException(ErrorCodes.MissingOrInvalidServiceCredentials,
                "OpenRouter API key is missing in integrations configuration.");
        }

        return new OpenAIClient(new ApiKeyCredential(config.OpenRouterApiKey), new OpenAIClientOptions
        {
            Endpoint = new Uri("https://openrouter.ai/api/v1")
        });
    }

    /// <inheritdoc />
    public async Task<string> GenerateAsync(
        string model,
        IEnumerable<PromptMessageDto> messages,
        CancellationToken cancellationToken = default)
    {
        var client = await GetOpenAiClientAsync();
        var chatClient = client.GetChatClient(model);
        
        var chatMessages = messages.Select(ToChatMessage).ToList();

        ClientResult<ChatCompletion> response;
        try
        {
            response = await chatClient.CompleteChatAsync(chatMessages, cancellationToken: cancellationToken);
        }
        catch (ClientResultException ex)
        {
            throw ToApiException(ex);
        }

        return response?.Value.Content[0].Text
            ?? throw new ApiException(ErrorCodes.ExternalServiceError, "OpenRouter returned no response.");
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> GenerateStreamedAsync(
        string model,
        IEnumerable<PromptMessageDto> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var client = await GetOpenAiClientAsync();
        var chatClient = client.GetChatClient(model);
        
        var chatMessages = messages.Select(ToChatMessage).ToList();

        var updates = chatClient.CompleteChatStreamingAsync(chatMessages, cancellationToken: cancellationToken);
        await using var updateEnumerator = updates.GetAsyncEnumerator(cancellationToken);

        while (true)
        {
            StreamingChatCompletionUpdate update;

            try
            {
                if (!await updateEnumerator.MoveNextAsync())
                {
                    break;
                }

                update = updateEnumerator.Current;
            }
            catch (ClientResultException ex)
            {
                throw ToApiException(ex);
            }

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

    /// <inheritdoc />
    public async Task<IEnumerable<TextGenerationModelInfo>> GetAvailableModelsAsync()
    {
        var client = await GetOpenAiClientAsync();
        var models = await client.GetOpenAIModelClient().GetModelsAsync();

        return models.Value.Select(m => new TextGenerationModelInfo
        {
            Id = m.Id
        });
    }

    private static ChatMessage ToChatMessage(PromptMessageDto message) =>
        message.Role switch
        {
            PromptMessageRole.User => new UserChatMessage(message.Message),
            PromptMessageRole.System => new SystemChatMessage(message.Message),
            PromptMessageRole.Assistant => new AssistantChatMessage(message.Message),
            _ => throw new NotSupportedException($"Unsupported message role: {message.Role}")
        };

    private static ApiException ToApiException(ClientResultException ex)
    {
        var message = $"OpenRouter request failed with status {ex.Status}: {ex.Message}";
        return new ApiException(ErrorCodes.ExternalServiceError, message);
    }
}
