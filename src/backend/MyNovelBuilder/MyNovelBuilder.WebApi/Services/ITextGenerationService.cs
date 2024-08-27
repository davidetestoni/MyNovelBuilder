using MyNovelBuilder.WebApi.Dtos.Prompt;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for generating text using LLMs.
/// </summary>
public interface ITextGenerationService
{
    /// <summary>
    /// Generate streamed text.
    /// </summary>
    IAsyncEnumerable<string> GenerateStreamedAsync(
        string model,
        IEnumerable<PromptMessageDto> messages,
        CancellationToken cancellationToken = default);
}
