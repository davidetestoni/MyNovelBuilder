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
    IAsyncEnumerable<string> GenerateStreamedAsync(IEnumerable<PromptMessageDto> messages);
}
