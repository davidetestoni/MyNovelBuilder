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
    /// A prompt for describing an image with compendium context.
    /// </summary>
    DescribeCompendiumImage = 6,

    /// <summary>
    /// A prompt for generating an image prompt for a compendium record.
    /// </summary>
    CreateCompendiumRecordImageGenerationPrompt = 7,

    /// <summary>
    /// A prompt for creating story events.
    /// </summary>
    CreateStoryEvents = 8,

    /// <summary>
    /// A prompt for translating sections of a novel.
    /// </summary>
    TranslateNovel = 9,

    /// <summary>
    /// A prompt for describing an image without compendium context.
    /// </summary>
    DescribeImage = 10,

    /// <summary>
    /// A prompt for preparing immersive multi-speaker TTS chunks for prose playback.
    /// </summary>
    PrepareImmersiveTts = 11,
}
