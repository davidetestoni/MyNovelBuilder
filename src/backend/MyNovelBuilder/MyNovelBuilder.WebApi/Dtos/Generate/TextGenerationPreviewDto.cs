using MyNovelBuilder.WebApi.Dtos.Prompt;

namespace MyNovelBuilder.WebApi.Dtos.Generate;

/// <summary>
/// DTO with some preview information about the text generation.
/// </summary>
public class TextGenerationPreviewDto
{
    /// <summary>
    /// The number of input tokens in the prompt.
    /// </summary>
    public required long InputTokens { get; set; }
    
    /// <summary>
    /// The IDs of the compendium records that will be included
    /// in the context for the text generation.
    /// </summary>
    public required IEnumerable<Guid> IncludedCompendiumRecordIds { get; set; }
    
    /// <summary>
    /// The final prompt messages that will be sent to the model
    /// for text generation, after processing the prompt and context information.
    /// </summary>
    public required IEnumerable<PromptMessageDto> FinalMessages { get; set; }
}
