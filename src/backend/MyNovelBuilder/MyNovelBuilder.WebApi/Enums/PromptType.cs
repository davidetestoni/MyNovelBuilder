namespace MyNovelBuilder.WebApi.Enums;

/// <summary>
/// The type of prompt.
/// </summary>
public enum PromptType
{
    /// <summary>
    /// A prompt to generate text.
    /// </summary>
    GenerateText,
    
    /// <summary>
    /// A prompt to summarize text.
    /// </summary>
    SummarizeText,
    
    /// <summary>
    /// A prompt to transform text, that takes the existing text as input.
    /// </summary>
    ReplaceText,
    
    /// <summary>
    /// A prompt to replace text, that takes the existing text and
    /// some additional context as input.
    /// </summary>
    ReplaceTextGuided,
    
    /// <summary>
    /// A prompt to create a compendium record.
    /// </summary>
    CreateCompendiumRecord,
    
    /// <summary>
    /// A prompt to edit a compendium record.
    /// </summary>
    EditCompendiumRecord
}
