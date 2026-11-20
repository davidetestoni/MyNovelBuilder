using System.ClientModel;
using System.Net.Http.Headers;
using System.ClientModel.Primitives;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Models.TextGeneration;
using OpenAI;
using OpenAI.Chat;
using MyNovelBuilder.WebApi.Attributes;
using MyNovelBuilder.WebApi.Models.Prompts;

namespace MyNovelBuilder.WebApi.Services.TextGeneration;

/// <summary>
/// Service for generating text using OpenRouter's OpenAI-compatible API.
/// </summary>
[RegisterKeyedService(TextGenerationProvider.OpenRouter, useHttpClient: true)]
public class OpenRouterTextGenerationService : ITextGenerationService
{
    private readonly HttpClient _httpClient;
    private readonly IIntegrationsService _integrationsService;
    private readonly OpenAIClient? _openAiClient = null;

    /// <summary></summary>
    public OpenRouterTextGenerationService(
        HttpClient httpClient,
        IIntegrationsService integrationsService)
    {
        _httpClient = httpClient;
        _integrationsService = integrationsService;
        _httpClient.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
    }
    
    private async ValueTask<OpenAIClient> GetOpenAiClientAsync(
        CancellationToken cancellationToken = default, ReasoningEffort? reasoningEffort = null)
    {
        if (_openAiClient is not null)
        {
            return _openAiClient;
        }

        var config = await _integrationsService.GetConfigAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(config.OpenRouterApiKey))
        {
            throw new ApiException(ErrorCodes.MissingOrInvalidServiceCredentials,
                "OpenRouter API key is missing in integrations configuration.");
        }

        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri("https://openrouter.ai/api/v1"),
            Transport = new HttpClientPipelineTransport(_httpClient)
        };
        var effort = reasoningEffort ?? config.TextGenerationReasoningEffort;
        if (effort != ReasoningEffort.ProviderDefault)
            options.AddPolicy(new OpenRouterReasoningPolicy(effort), PipelinePosition.PerCall);
        return new OpenAIClient(new ApiKeyCredential(config.OpenRouterApiKey), options);
    }

    /// <inheritdoc />
    public async Task<string> GenerateAsync(
        string model,
        IEnumerable<PromptMessage> messages,
        StructuredOutputOptions? structuredOutputOptions = null,
        CancellationToken cancellationToken = default)
    {
        var config = await _integrationsService.GetConfigAsync(cancellationToken);
        return await GenerateWithReasoningAsync(model, messages, config.TextGenerationReasoningEffort,
            structuredOutputOptions, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> GenerateWithReasoningAsync(
        string model,
        IEnumerable<PromptMessage> messages,
        ReasoningEffort reasoningEffort,
        StructuredOutputOptions? structuredOutputOptions = null,
        CancellationToken cancellationToken = default)
    {
        var client = await GetOpenAiClientAsync(cancellationToken, reasoningEffort);
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

        return GetResponseText(response?.Value);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> GenerateStreamedAsync(
        string model,
        IEnumerable<PromptMessage> messages,
        StructuredOutputOptions? structuredOutputOptions = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var client = await GetOpenAiClientAsync(cancellationToken);
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
        IEnumerable<PromptMessage> messages,
        byte[] imageBytes,
        string imageMimeType,
        CancellationToken cancellationToken = default)
    {
        var client = await GetOpenAiClientAsync(cancellationToken);
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

        return GetResponseText(response?.Value);
    }

    internal static string GetResponseText(ChatCompletion? completion)
    {
        if (completion is null)
        {
            throw new ApiException(ErrorCodes.ExternalServiceError, "OpenRouter returned no response.");
        }

        if (!string.IsNullOrWhiteSpace(completion.Refusal))
        {
            throw new ApiException(ErrorCodes.ExternalServiceError,
                $"OpenRouter refused to generate text: {completion.Refusal}");
        }

        var text = string.Concat(completion.Content.Select(part => part.Text));
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ApiException(ErrorCodes.ExternalServiceError,
                $"OpenRouter returned no text content (finish reason: {completion.FinishReason}).");
        }

        return text;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TextGenerationModelInfo>> GetAvailableModelsAsync(
        CancellationToken cancellationToken = default)
    {
        var config = await _integrationsService.GetConfigAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(config.OpenRouterApiKey))
        {
            throw new ApiException(ErrorCodes.MissingOrInvalidServiceCredentials,
                "OpenRouter API key is missing in integrations configuration.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "models");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.OpenRouterApiKey);
        
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

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
            SupportsStructuredOutputs = SupportsStructuredOutputs(m),
            InputTokenPrice = decimal.Parse(m?["pricing"]?["prompt"]?.GetValue<string>() ?? "0"),
            OutputTokenPrice = decimal.Parse(m?["pricing"]?["completion"]?.GetValue<string>() ?? "0")
        });
    }

    private static ChatMessage ToChatMessage(PromptMessage message) =>
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
