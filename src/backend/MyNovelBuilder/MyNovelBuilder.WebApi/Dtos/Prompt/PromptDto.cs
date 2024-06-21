using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Dtos.Prompt;

/// <summary>
/// Data transfer object for a prompt.
/// </summary>
public class PromptDto
{
    /// <summary>
    /// The prompt's ID.
    /// </summary>
    public required Guid Id { get; set; }
    
    /// <summary>
    /// The date and time the prompt was created.
    /// </summary>
    public required DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// The date and time the prompt was last updated.
    /// </summary>
    public required DateTime UpdatedAt { get; set; }
    
    /// <summary>
    /// The prompt's name.
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// The prompt type.
    /// </summary>
    public required PromptType Type { get; set; }
    
    /// <summary>
    /// The prompt's messages.
    /// </summary>
    public required IEnumerable<PromptMessageDto> Messages { get; set; }
}
