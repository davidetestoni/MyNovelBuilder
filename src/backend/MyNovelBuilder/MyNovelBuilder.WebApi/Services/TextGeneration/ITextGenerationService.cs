using MyNovelBuilder.WebApi.Dtos.Prompt;
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
        IEnumerable<PromptMessageDto> messages,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generate streamed text.
    /// </summary>
    IAsyncEnumerable<string> GenerateStreamedAsync(
        string model,
        IEnumerable<PromptMessageDto> messages,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get available text generation models.
    /// </summary>
    Task<IEnumerable<TextGenerationModelInfo>> GetAvailableModelsAsync();
}
