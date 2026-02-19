using System.Runtime.CompilerServices;
using Google.GenAI.Types;
using MyNovelBuilder.WebApi.Dtos.Prompt;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Models.TextGeneration;
using MyNovelBuilder.WebApi.Attributes;

namespace MyNovelBuilder.WebApi.Services.TextGeneration;

/// <summary>
/// Service for generating text using Google's GenAI API.
/// </summary>
[RegisterKeyedService(TextGenerationProvider.GoogleGenAi)]
public class GoogleGenAiTextGenerationService : ITextGenerationService
{
    private readonly IIntegrationsService _integrationsService;
    private readonly Google.GenAI.Client? _googleGenAiClient = null;

    /// <summary></summary>
    public GoogleGenAiTextGenerationService(
        IIntegrationsService integrationsService)
    {
        _integrationsService = integrationsService;
    }
    
    private async ValueTask<Google.GenAI.Client> GetGoogleGenAiClientAsync()
    {
        if (_googleGenAiClient is not null)
        {
            return _googleGenAiClient;
        }

        var config = await _integrationsService.GetConfigAsync();

        if (string.IsNullOrWhiteSpace(config.GoogleGenAiApiKey))
        {
            throw new ApiException(ErrorCodes.MissingOrInvalidServiceCredentials,
                "Google API key is missing in integrations configuration.");
        }

        return new Google.GenAI.Client(apiKey: config.GoogleGenAiApiKey);
    }
    
    /// <inheritdoc />
    public async Task<string> GenerateAsync(
        string model,
        IEnumerable<PromptMessageDto> messages,
        StructuredOutputOptions? structuredOutputOptions = null,
        CancellationToken cancellationToken = default)
    {
        var client = await GetGoogleGenAiClientAsync();
        
        var messageList = messages.ToList();
        var systemPrompt = messageList.FirstOrDefault(m => m.Role is PromptMessageRole.System);
        
        var response = await client.Models.GenerateContentAsync(
            model,
            messageList
                .Where(m => m.Role is not PromptMessageRole.System)
                .Select(ToContent)
                .ToList(),
            CreateGenerateContentConfig(systemPrompt, structuredOutputOptions));

        return response.Candidates![0].Content!.Parts![0].Text!;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> GenerateStreamedAsync(string model,
        IEnumerable<PromptMessageDto> messages,
        StructuredOutputOptions? structuredOutputOptions = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var client = await GetGoogleGenAiClientAsync();
        
        var messageList = messages.ToList();
        var systemPrompt = messageList.FirstOrDefault(m => m.Role is PromptMessageRole.System);
        var conversationMessages = messageList
            .Where(m => m.Role is not PromptMessageRole.System)
            .Select(ToContent)
            .ToList();
        
        var config = CreateGenerateContentConfig(systemPrompt, structuredOutputOptions);

        await foreach (var chunk in client.Models.GenerateContentStreamAsync(
                           model, conversationMessages, config).WithCancellation(cancellationToken))
        {
            var candidate = chunk.Candidates?[0]!;

            if (candidate.FinishReason is not (null or Google.GenAI.Types.FinishReason.STOP))
            {
                throw new ApiException(ErrorCodes.ExternalServiceError,
                    $"Google GenAI refused to generate text. " +
                    $"{candidate.FinishReason}: {candidate.FinishMessage}");
            }
            
            yield return candidate.Content!.Parts![0].Text!;
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
        cancellationToken.ThrowIfCancellationRequested();
        var client = await GetGoogleGenAiClientAsync();

        var messageList = messages.ToList();
        var systemPrompt = messageList.FirstOrDefault(m => m.Role is PromptMessageRole.System);
        var lastUserMessageIndex = messageList.FindLastIndex(m => m.Role is PromptMessageRole.User);

        var conversationMessages = new List<Google.GenAI.Types.Content>();
        for (var i = 0; i < messageList.Count; i++)
        {
            var message = messageList[i];
            if (message.Role is PromptMessageRole.System)
            {
                continue;
            }

            if (message.Role is PromptMessageRole.User && i == lastUserMessageIndex)
            {
                conversationMessages.Add(new Google.GenAI.Types.Content
                {
                    Role = "user",
                    Parts =
                    [
                        ToPart(message.Message),
                        new Google.GenAI.Types.Part
                        {
                            InlineData = new Blob
                            {
                                Data = imageBytes,
                                MimeType = imageMimeType
                            }
                        }
                    ]
                });

                continue;
            }

            conversationMessages.Add(ToContent(message));
        }

        if (lastUserMessageIndex < 0)
        {
            conversationMessages.Add(new Google.GenAI.Types.Content
            {
                Role = "user",
                Parts =
                [
                    ToPart("Please describe this image."),
                    new Google.GenAI.Types.Part
                    {
                        InlineData = new Blob
                        {
                            Data = imageBytes,
                            MimeType = imageMimeType
                        }
                    }
                ]
            });
        }

        var response = await client.Models.GenerateContentAsync(
            model,
            conversationMessages,
            new Google.GenAI.Types.GenerateContentConfig
            {
                SystemInstruction = systemPrompt is null
                    ? null
                    : new Google.GenAI.Types.Content
                    {
                        Parts = [ToPart(systemPrompt.Message)],
                        Role = "model"
                    }
            });

        var text = response.Candidates?
            .SelectMany(c => c.Content?.Parts ?? [])
            .Select(p => p.Text)
            .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));

        return text ?? throw new ApiException(ErrorCodes.ExternalServiceError,
            "Google GenAI returned no response.");
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TextGenerationModelInfo>> GetAvailableModelsAsync()
    {
        var client = await GetGoogleGenAiClientAsync();
        var models = new List<TextGenerationModelInfo>();
        
        var pager = await client.Models.ListAsync(new ListModelsConfig
        {
            PageSize = 1000
        });
        models.AddRange(pager.CurrentPage
            .Where(m => m.SupportedActions is not null && m.SupportedActions.Contains("generateContent"))
            .Select(m => new TextGenerationModelInfo
        {
            Id = m.Name!,
            IsVisionCapable = IsVisionCapable(m.Name),
            SupportsStructuredOutputs = SupportsStructuredOutputs(m.Name)
        }));

        while (pager.HasMorePages)
        {
            await pager.NextPageAsync();
            models.AddRange(pager.CurrentPage
                .Where(m => m.SupportedActions is not null && m.SupportedActions.Contains("generateContent"))
                .Select(m => new TextGenerationModelInfo
            {
                Id = m.Name!,
                IsVisionCapable = IsVisionCapable(m.Name),
                SupportsStructuredOutputs = SupportsStructuredOutputs(m.Name)
            }));
        }
        
        return models;
    }

    private static Google.GenAI.Types.Content ToContent(PromptMessageDto message) =>
        new()
        {
            Parts = [ToPart(message.Message)],
            Role = message.Role switch
            {
                PromptMessageRole.User => "user",
                PromptMessageRole.Assistant => "model",
                PromptMessageRole.System => throw new InvalidOperationException(
                    "System messages should be handled separately and not included in content parts."),
                _ => throw new InvalidOperationException(
                    $"Unsupported prompt message role: {message.Role}")
            }
        };
    
    private static Google.GenAI.Types.Part ToPart(string text) =>
        new() { Text = text };

    private static bool IsVisionCapable(string? modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return false;
        }

        var value = modelName.ToLowerInvariant();
        return value.StartsWith("models/gemini-")
               && !value.Contains("embedding");
    }

    private static bool SupportsStructuredOutputs(string? modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return false;
        }

        // Google JSON-schema structured output is supported for Gemini models.
        var value = modelName.ToLowerInvariant();
        return value.StartsWith("models/gemini-")
               && !value.Contains("embedding")
               && !value.Contains("tts")
               && !value.Contains("native-audio")
               && !value.Contains("live");
    }

    private static GenerateContentConfig CreateGenerateContentConfig(
        PromptMessageDto? systemPrompt,
        StructuredOutputOptions? structuredOutputOptions = null)
    {
        var config = new GenerateContentConfig
        {
            SystemInstruction = systemPrompt is null
                ? null
                : new Content
                {
                    Parts = [ToPart(systemPrompt.Message)],
                    Role = "model"
                }
        };

        if (structuredOutputOptions is null)
        {
            return config;
        }

        config.ResponseMimeType = "application/json";
        config.ResponseJsonSchema = structuredOutputOptions.JsonSchema;
        return config;
    }
}
