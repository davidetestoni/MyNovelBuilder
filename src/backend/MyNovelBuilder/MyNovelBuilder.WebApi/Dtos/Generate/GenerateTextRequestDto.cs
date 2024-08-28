namespace MyNovelBuilder.WebApi.Dtos.Generate;

/// <summary>
/// DTO for a request to generate text.
/// </summary>
public class GenerateTextRequestDto
{
    /// <summary>
    /// The model to use for text generation.
    /// </summary>
    public required string Model { get; set; }
    
    /// <summary>
    /// The context for the text generation.
    /// </summary>
    public string? Context { get; set; }
    
    /// <summary>
    /// The instructions for the text generation.
    /// </summary>
    public string? Instructions { get; set; }
    
    /// <summary>
    /// The prompt ID.
    /// </summary>
    public Guid PromptId { get; set; }
    
    /// <summary>
    /// The novel ID.
    /// </summary>
    public Guid NovelId { get; set; }
}
