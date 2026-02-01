using System.Runtime.CompilerServices;
using Google.GenAI.Types;
using MyNovelBuilder.WebApi.Dtos.Prompt;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Models.TextGeneration;
using OpenAI.Models;

namespace MyNovelBuilder.WebApi.Services.TextGeneration;

/// <summary>
/// Service for generating text using Google's GenAI API.
/// </summary>
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
            new Google.GenAI.Types.GenerateContentConfig
            {
                SystemInstruction = systemPrompt is null
                    ? null 
                    : new Google.GenAI.Types.Content
                {
                    Parts = [ToPart(systemPrompt.Message)],
                    Role = "model"
                }
            }
            );

        return response.Candidates![0].Content!.Parts![0].Text!;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> GenerateStreamedAsync(string model,
        IEnumerable<PromptMessageDto> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var client = await GetGoogleGenAiClientAsync();
        
        var messageList = messages.ToList();
        var systemPrompt = messageList.FirstOrDefault(m => m.Role is PromptMessageRole.System);
        var conversationMessages = messageList
            .Where(m => m.Role is not PromptMessageRole.System)
            .Select(ToContent)
            .ToList();
        
        var config = new Google.GenAI.Types.GenerateContentConfig
        {
            SystemInstruction = systemPrompt is null
                ? null 
                : new Google.GenAI.Types.Content
            {
                Parts = [ToPart(systemPrompt.Message)],
                Role = "model"
            }
        };

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
    public async Task<IEnumerable<TextGenerationModelInfo>> GetAvailableModelsAsync()
    {
        // TODO: Cache these
        
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
            Id = m.Name!
        }));

        while (pager.HasMorePages)
        {
            await pager.NextPageAsync();
            models.AddRange(pager.CurrentPage
                .Where(m => m.SupportedActions is not null && m.SupportedActions.Contains("generateContent"))
                .Select(m => new TextGenerationModelInfo
            {
                Id = m.Name!
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
}