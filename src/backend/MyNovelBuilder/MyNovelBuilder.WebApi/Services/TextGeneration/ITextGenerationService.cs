using MyNovelBuilder.WebApi.Dtos.Prompt;
using MyNovelBuilder.WebApi.Models.Prompts;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Models.TextGeneration;

namespace MyNovelBuilder.WebApi.Services.TextGeneration;

/// <summary>
/// Service for generating text using LLMs.
/// </summary>
public interface ITextGenerationService
{
    /// <summary>
    /// Generate text.
    /// </summary>
    Task<string> GenerateAsync(
        string model,
        IEnumerable<PromptMessage> messages,
        StructuredOutputOptions? structuredOutputOptions = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generate streamed text.
    /// </summary>
    IAsyncEnumerable<string> GenerateStreamedAsync(
        string model,
        IEnumerable<PromptMessage> messages,
        StructuredOutputOptions? structuredOutputOptions = null,
        CancellationToken cancellationToken = default);

    /// <summary>Generate text with an explicit reasoning effort, overriding integration defaults.</summary>
    Task<string> GenerateWithReasoningAsync(
        string model,
        IEnumerable<PromptMessage> messages,
        ReasoningEffort reasoningEffort,
        StructuredOutputOptions? structuredOutputOptions = null,
        CancellationToken cancellationToken = default) =>
        GenerateAsync(model, messages, structuredOutputOptions, cancellationToken);

    /// <summary>
    /// Describe an image.
    /// </summary>
    Task<string> DescribeImageAsync(
        string model,
        IEnumerable<PromptMessage> messages,
        byte[] imageBytes,
        string imageMimeType,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get available text generation models.
    /// </summary>
    Task<IEnumerable<TextGenerationModelInfo>> GetAvailableModelsAsync(
        CancellationToken cancellationToken = default);
}
