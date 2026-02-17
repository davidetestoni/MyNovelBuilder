using System.Text.Json.Serialization;
using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Dtos.Generate;

/// <summary>
/// DTO for the context information for text generation.
/// </summary>
[JsonDerivedType(typeof(GenerateTextContextInfoDto), typeDiscriminator: "generateText")]
[JsonDerivedType(typeof(SummarizeTextContextInfoDto), typeDiscriminator: "summarizeText")]
[JsonDerivedType(typeof(ReplaceTextContextInfoDto), typeDiscriminator: "replaceText")]
[JsonDerivedType(typeof(CreateCompendiumRecordContextInfoDto), typeDiscriminator: "createCompendiumRecord")]
[JsonDerivedType(typeof(EditCompendiumRecordContextInfoDto), typeDiscriminator: "editCompendiumRecord")]
[JsonDerivedType(typeof(SendChatMessageContextInfoDto), typeDiscriminator: "sendChatMessage")]
public class TextGenerationContextInfoDto
{
    
}

/// <summary>
/// DTO for the context information for text generation.
/// </summary>
public class GenerateTextContextInfoDto : TextGenerationContextInfoDto
{
    /// <summary>
    /// The index of the chapter.
    /// </summary>
    public int ChapterIndex { get; set; }
    
    /// <summary>
    /// The index of the section.
    /// </summary>
    public int SectionIndex { get; set; }
    
    /// <summary>
    /// The offset of the text in the section.
    /// </summary>
    public int TextOffset { get; set; }
    
    /// <summary>
    /// The instructions for the text generation.
    /// </summary>
    public string? Instructions { get; set; }
}

/// <summary>
/// DTO for the context information for text summarization.
/// </summary>
public class SummarizeTextContextInfoDto : TextGenerationContextInfoDto
{
    /// <summary>
    /// The index of the chapter.
    /// </summary>
    public int ChapterIndex { get; set; }
    
    /// <summary>
    /// The index of the section.
    /// </summary>
    public int SectionIndex { get; set; }
}

/// <summary>
/// DTO for the context information for text replacement.
/// </summary>
public class ReplaceTextContextInfoDto : TextGenerationContextInfoDto
{
    /// <summary>
    /// The index of the chapter.
    /// </summary>
    public int ChapterIndex { get; set; }
    
    /// <summary>
    /// The index of the section.
    /// </summary>
    public int SectionIndex { get; set; }
    
    /// <summary>
    /// The offset of the text to replace in the section.
    /// </summary>
    public int TextOffset { get; set; }
    
    /// <summary>
    /// The length of the text to replace.
    /// </summary>
    public int TextLength { get; set; }
    
    /// <summary>
    /// The instructions for the text generation.
    /// </summary>
    public string? Instructions { get; set; }
}

/// <summary>
/// DTO for the context information for creating a compendium record.
/// </summary>
public class CreateCompendiumRecordContextInfoDto : TextGenerationContextInfoDto
{
    /// <summary>
    /// The index of the chapter.
    /// </summary>
    public int ChapterIndex { get; set; }
    
    /// <summary>
    /// The index of the section.
    /// </summary>
    public int SectionIndex { get; set; }
    
    /// <summary>
    /// The offset of the text to use for the compendium record.
    /// </summary>
    public int TextOffset { get; set; }
    
    /// <summary>
    /// The length of the text to use for the compendium record.
    /// </summary>
    public int TextLength { get; set; }
    
    /// <summary>
    /// The instructions about what record to generate.
    /// </summary>
    public string? Instructions { get; set; }
}

/// <summary>
/// DTO for the context information for editing a compendium record.
/// </summary>
public class EditCompendiumRecordContextInfoDto : TextGenerationContextInfoDto
{
    /// <summary>
    /// The index of the chapter.
    /// </summary>
    public int ChapterIndex { get; set; }
    
    /// <summary>
    /// The index of the section.
    /// </summary>
    public int SectionIndex { get; set; }
    
    /// <summary>
    /// The offset of the text to use for the compendium record.
    /// </summary>
    public int TextOffset { get; set; }
    
    /// <summary>
    /// The length of the text to use for the compendium record.
    /// </summary>
    public int TextLength { get; set; }
    
    /// <summary>
    /// The ID of the compendium record to edit.
    /// </summary>
    public Guid RecordId { get; set; }
    
    /// <summary>
    /// The instructions for the text generation.
    /// </summary>
    public string? Instructions { get; set; }
}

/// <summary>
/// DTO for a chat message.
/// </summary>
public class ChatMessageDto
{
    /// <summary>
    /// The role of the sender of the chat message.
    /// </summary>
    public required ChatMessageRole Role { get; set; }
    
    /// <summary>
    /// The text content of the chat message.
    /// </summary>
    public required string TextContent { get; set; }
}

/// <summary>
/// DTO for the context information for sending a chat message.
/// </summary>
public class SendChatMessageContextInfoDto : TextGenerationContextInfoDto
{
    /// <summary>
    /// The id of the chapter associated with the chat context.
    /// If null, the context will include the entire novel.
    /// </summary>
    public int? ChapterIndex { get; set; }
    
    /// <summary>
    /// The user message to which the AI is responding.
    /// </summary>
    public string UserMessage { get; set; } = string.Empty;
    
    /// <summary>
    /// The messages in the chat conversation prior to the user message.
    /// </summary>
    public IEnumerable<ChatMessageDto> PreviousMessages { get; set; } = new List<ChatMessageDto>();
    
    /// <summary>
    /// The ids of the compendia included in the chat context.
    /// All records from these compendia are considered when generating responses.
    /// The compendia must belong to the specified novel.
    /// </summary>
    public IEnumerable<Guid> CompendiumIds { get; set; } = new List<Guid>();
    
    /// <summary>
    /// The ids of the specific compendium records included in the chat context.
    /// The records must belong to compendia associated with the specified novel.
    /// </summary>
    public IEnumerable<Guid> CompendiumRecordIds { get; set; } = new List<Guid>();
}
