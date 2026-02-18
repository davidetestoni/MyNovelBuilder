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
    
    /// <summary>
    /// A system prompt for chat interactions.
    /// </summary>
    SendChatMessage = 5,
    
    /// <summary>
    /// A prompt for describing an image.
    /// </summary>
    DescribeImage = 6,

    /// <summary>
    /// A prompt for generating an image prompt for a compendium record.
    /// </summary>
    CreateCompendiumRecordImageGenerationPrompt = 7,
}
