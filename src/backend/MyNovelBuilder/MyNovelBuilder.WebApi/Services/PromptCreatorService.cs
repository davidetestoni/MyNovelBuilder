using MyNovelBuilder.WebApi.Dtos.Generate;
using MyNovelBuilder.WebApi.Dtos.Prompt;
using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for creating prompts.
/// </summary>
public class PromptCreatorService : IPromptCreatorService
{
    /// <inheritdoc />
    public async Task<IEnumerable<PromptMessageDto>> CreatePromptAsync(GenerateTextRequestDto request)
    {
        // TODO: Mocked for testing purposes.
        await Task.Delay(100);
        return new[]
        {
            new PromptMessageDto
            {
                Role = PromptMessageRole.System,
                Message = "You are a helpful assistant. Answer the user's questions and provide information.",
            },
            new PromptMessageDto
            {
                Role = PromptMessageRole.User,
                Message = "Hi, how are you?",
            }
        };
    }
}
