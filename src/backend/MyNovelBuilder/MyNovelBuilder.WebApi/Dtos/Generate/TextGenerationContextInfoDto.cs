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
    /// The name of the compendium record to create.
    /// </summary>
    public required string RecordName { get; set; }
    
    /// <summary>
    /// The type of the compendium record to create.
    /// </summary>
    public CompendiumRecordType RecordType { get; set; }
    
    /// <summary>
    /// The ID of the compendium to create the record in.
    /// </summary>
    public Guid CompendiumId { get; set; }
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