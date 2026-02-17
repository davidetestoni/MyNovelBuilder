namespace MyNovelBuilder.WebApi.Dtos.Generate;

/// <summary>
/// DTO for a compendium-scoped request to generate text.
/// </summary>
public class CompendiumGenerateTextRequestDto
{
    /// <summary>
    /// The model to use for text generation.
    /// </summary>
    public required string Model { get; set; }
    
    /// <summary>
    /// The prompt ID.
    /// </summary>
    public Guid PromptId { get; set; }
    
    /// <summary>
    /// The compendium ID.
    /// </summary>
    public Guid CompendiumId { get; set; }
    
    /// <summary>
    /// The context information.
    /// </summary>
    public required CompendiumTextGenerationContextInfoDto ContextInfo { get; set; }
}
