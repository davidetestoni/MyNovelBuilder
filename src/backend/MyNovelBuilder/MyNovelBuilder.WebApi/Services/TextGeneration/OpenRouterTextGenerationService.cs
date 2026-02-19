using System.ClientModel;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
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
        StructuredOutputOptions? structuredOutputOptions = null,
        CancellationToken cancellationToken = default)
    {
        var client = await GetOpenAiClientAsync();
        var chatClient = client.GetChatClient(model);
        
        var chatMessages = messages.Select(ToChatMessage).ToList();
        var completionOptions = CreateChatCompletionOptions(structuredOutputOptions);

        ClientResult<ChatCompletion> response;
        try
        {
            response = await chatClient.CompleteChatAsync(
                chatMessages,
                completionOptions,
                cancellationToken: cancellationToken);
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
        StructuredOutputOptions? structuredOutputOptions = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var client = await GetOpenAiClientAsync();
        var chatClient = client.GetChatClient(model);
        
        var chatMessages = messages.Select(ToChatMessage).ToList();
        var completionOptions = CreateChatCompletionOptions(structuredOutputOptions);

        var updates = chatClient.CompleteChatStreamingAsync(
            chatMessages,
            completionOptions,
            cancellationToken: cancellationToken);
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
    public async Task<string> DescribeImageAsync(
        string model,
        IEnumerable<PromptMessageDto> messages,
        byte[] imageBytes,
        string imageMimeType,
        CancellationToken cancellationToken = default)
    {
        var client = await GetOpenAiClientAsync();
        var chatClient = client.GetChatClient(model);

        var promptMessages = messages.ToList();
        var lastUserMessageIndex = promptMessages.FindLastIndex(m => m.Role is PromptMessageRole.User);
        var chatMessages = new List<ChatMessage>();

        for (var i = 0; i < promptMessages.Count; i++)
        {
            var message = promptMessages[i];

            if (message.Role is PromptMessageRole.User && i == lastUserMessageIndex)
            {
                chatMessages.Add(new UserChatMessage(new List<ChatMessageContentPart>
                {
                    ChatMessageContentPart.CreateTextPart(message.Message),
                    ChatMessageContentPart.CreateImagePart(BinaryData.FromBytes(imageBytes), imageMimeType)
                }));
                continue;
            }

            chatMessages.Add(ToChatMessage(message));
        }

        if (lastUserMessageIndex < 0)
        {
            chatMessages.Add(new UserChatMessage(new List<ChatMessageContentPart>
            {
                ChatMessageContentPart.CreateTextPart("Please describe this image."),
                ChatMessageContentPart.CreateImagePart(BinaryData.FromBytes(imageBytes), imageMimeType)
            }));
        }

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
    public async Task<IEnumerable<TextGenerationModelInfo>> GetAvailableModelsAsync()
    {
        var config = await _integrationsService.GetConfigAsync();

        if (string.IsNullOrWhiteSpace(config.OpenRouterApiKey))
        {
            throw new ApiException(ErrorCodes.MissingOrInvalidServiceCredentials,
                "OpenRouter API key is missing in integrations configuration.");
        }

        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://openrouter.ai/api/v1/")
        };
        using var request = new HttpRequestMessage(HttpMethod.Get, "models");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.OpenRouterApiKey);
        
        using var response = await httpClient.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new ApiException(ErrorCodes.ExternalServiceError,
                $"OpenRouter models request failed with status {(int)response.StatusCode}.");
        }

        var root = JsonNode.Parse(json);
        var data = root?["data"]?.AsArray() ?? [];

        return data
            .Where(m => !string.IsNullOrWhiteSpace(m?["id"]?.GetValue<string>()))
            .Select(m => new TextGenerationModelInfo
        {
            Id = m?["id"]?.GetValue<string>() ?? string.Empty,
            IsVisionCapable = HasImageInputModality(m),
            SupportsStructuredOutputs = SupportsStructuredOutputs(m)
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

    private static ChatCompletionOptions CreateChatCompletionOptions(
        StructuredOutputOptions? structuredOutputOptions)
    {
        var options = new ChatCompletionOptions();

        if (structuredOutputOptions is null)
        {
            return options;
        }

        options.ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
            structuredOutputOptions.SchemaName,
            jsonSchema: BinaryData.FromString(structuredOutputOptions.JsonSchema),
            jsonSchemaIsStrict: structuredOutputOptions.Strict);

        return options;
    }

    private static ApiException ToApiException(ClientResultException ex)
    {
        var message = $"OpenRouter request failed with status {ex.Status}: {ex.Message}";
        return new ApiException(ErrorCodes.ExternalServiceError, message);
    }

    private static bool HasImageInputModality(JsonNode? model)
    {
        var modalities = model?["architecture"]?["input_modalities"]?.AsArray();
        if (modalities is null)
        {
            return false;
        }

        return modalities.Any(m =>
            string.Equals(m?.GetValue<string>(), "image", StringComparison.OrdinalIgnoreCase));
    }

    private static bool SupportsStructuredOutputs(JsonNode? model)
    {
        var parameters = model?["supported_parameters"]?.AsArray();
        if (parameters is null)
        {
            return false;
        }

        return parameters.Any(p =>
            string.Equals(p?.GetValue<string>(), "structured_outputs", StringComparison.OrdinalIgnoreCase));
    }
}
