namespace MyNovelBuilder.WebApi.Enums;

/// <summary>
/// The type of prompt.
/// </summary>
public enum PromptType
{
    /// <summary>
    /// A prompt to generate text.
    /// </summary>
    GenerateText = 0,
    
    /// <summary>
    /// A prompt to summarize text.
    /// </summary>
    SummarizeText = 1,
    
    /// <summary>
    /// A prompt to replace text.
    /// </summary>
    ReplaceText = 2,
    
    /// <summary>
    /// A prompt to create a compendium record.
    /// </summary>
    CreateCompendiumRecord = 3,
    
    /// <summary>
    /// A prompt to edit a compendium record.
    /// </summary>
    EditCompendiumRecord = 4,
}
