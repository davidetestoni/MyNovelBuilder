using System.Text.Json.Serialization;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Models.TextGeneration;

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
[JsonDerivedType(typeof(DescribeImageContextInfoDto), typeDiscriminator: "describeImage")]
[JsonDerivedType(typeof(DescribeCompendiumImageContextInfoDto), typeDiscriminator: "describeCompendiumImage")]
[JsonDerivedType(typeof(CreateCompendiumRecordImageGenerationPromptContextInfoDto), typeDiscriminator: "createCompendiumRecordImageGenerationPrompt")]
[JsonDerivedType(typeof(PrepareImmersiveTtsContextInfoDto), typeDiscriminator: "prepareImmersiveTts")]
[JsonDerivedType(typeof(CreateStoryEventsContextInfoDto), typeDiscriminator: "createStoryEvents")]
[JsonDerivedType(typeof(SuggestStoryDevelopmentsContextInfoDto), typeDiscriminator: "suggestStoryDevelopments")]
[JsonDerivedType(typeof(TranslateNovelContextInfoDto), typeDiscriminator: "translateNovel")]
public abstract class TextGenerationContextInfoDto
{
    /// <summary>
    /// Gets structured output options for this context, if any.
    /// </summary>
    public virtual StructuredOutputOptions? GetStructuredOutputOptions() => null;
}

/// <summary>
/// DTO for the context information for text generation that is scoped to a specific novel.
/// </summary>
public abstract class NovelTextGenerationContextInfoDto : TextGenerationContextInfoDto
{
    /// <summary>
    /// The ID of the novel.
    /// </summary>
    public required Guid NovelId { get; set; }
}

/// <summary>
/// DTO for the context information for text generation that is scoped to a specific compendium.
/// </summary>
public abstract class CompendiumTextGenerationContextInfoDto : TextGenerationContextInfoDto
{
    /// <summary>
    /// The ID of the compendium.
    /// </summary>
    public required Guid CompendiumId { get; set; }
}

/// <summary>
/// DTO for the context information for text generation that is not scoped to a novel or compendium.
/// </summary>
public abstract class GenericTextGenerationContextInfoDto : TextGenerationContextInfoDto
{
}

/// <summary>
/// DTO for the context information for text generation.
/// </summary>
public class GenerateTextContextInfoDto : NovelTextGenerationContextInfoDto
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
public class SummarizeTextContextInfoDto : NovelTextGenerationContextInfoDto
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
public class ReplaceTextContextInfoDto : NovelTextGenerationContextInfoDto
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
public class CreateCompendiumRecordContextInfoDto : NovelTextGenerationContextInfoDto
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
public class EditCompendiumRecordContextInfoDto : NovelTextGenerationContextInfoDto
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
public class SendChatMessageContextInfoDto : NovelTextGenerationContextInfoDto
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

/// <summary>
/// DTO for the context information for image description.
/// </summary>
public class DescribeImageContextInfoDto : GenericTextGenerationContextInfoDto
{
    /// <summary>
    /// Additional instructions for the image description.
    /// </summary>
    public string? Instructions { get; set; }
}

/// <summary>
/// DTO for the context information for image description.
/// </summary>
public class DescribeCompendiumImageContextInfoDto : CompendiumTextGenerationContextInfoDto
{
    /// <summary>
    /// Additional instructions for the image description.
    /// </summary>
    public string? Instructions { get; set; }
}

/// <summary>
/// DTO for the context information for generating an image generation prompt for a compendium record.
/// </summary>
public class CreateCompendiumRecordImageGenerationPromptContextInfoDto : CompendiumTextGenerationContextInfoDto
{
    /// <summary>
    /// The ID of the compendium record for which to generate an image generation prompt.
    /// </summary>
    public Guid CompendiumRecordId { get; set; }
    
    /// <summary>
    /// Any additional instructions for the image generation prompt generation.
    /// </summary>
    public string? Instructions { get; set; }
}

/// <summary>
/// DTO for the context information for preparing immersive TTS chunks.
/// </summary>
public class PrepareImmersiveTtsContextInfoDto : NovelTextGenerationContextInfoDto
{
    private const string _immersiveTtsJsonSchema = """
                                                   {
                                                     "type": "array",
                                                     "items": {
                                                       "type": "object",
                                                       "properties": {
                                                         "speakerKind": {
                                                           "type": "string",
                                                           "enum": ["narrator", "character"]
                                                         },
                                                         "speakerName": { "type": "string" },
                                                         "characterRecordId": {
                                                           "type": ["string", "null"]
                                                         },
                                                         "text": { "type": "string" }
                                                       },
                                                       "required": ["speakerKind", "speakerName", "characterRecordId", "text"],
                                                       "additionalProperties": false
                                                     }
                                                   }
                                                   """;

    /// <summary>
    /// The index of the chapter.
    /// </summary>
    public int ChapterIndex { get; set; }

    /// <summary>
    /// The index of the section to prepare.
    /// </summary>
    public int SectionIndex { get; set; }

    /// <summary>
    /// The active TTS provider used for character-voice resolution.
    /// </summary>
    public TtsProvider Provider { get; set; }

    /// <summary>
    /// The active TTS model used for character-voice resolution.
    /// </summary>
    public required string TtsModelId { get; set; }

    /// <inheritdoc />
    public override StructuredOutputOptions? GetStructuredOutputOptions() => new()
    {
        SchemaName = "immersive_tts_chunks",
        JsonSchema = _immersiveTtsJsonSchema,
        Strict = true
    };
}

/// <summary>
/// DTO for the context information for creating story events.
/// </summary>
public class CreateStoryEventsContextInfoDto : NovelTextGenerationContextInfoDto
{
    private const string _storyEventsJsonSchema = """
                                                  {
                                                    "type": "array",
                                                    "items": {
                                                      "type": "object",
                                                      "properties": {
                                                        "title": { "type": "string" },
                                                        "date": { "type": "string" },
                                                        "description": { "type": "string" }
                                                      },
                                                      "required": ["title", "date", "description"],
                                                      "additionalProperties": false
                                                    }
                                                  }
                                                  """;

    /// <summary>
    /// The index of the chapter.
    /// </summary>
    public int ChapterIndex { get; set; }

    /// <inheritdoc />
    public override StructuredOutputOptions? GetStructuredOutputOptions() => new()
    {
        SchemaName = "story_events",
        JsonSchema = _storyEventsJsonSchema,
        Strict = true
    };
}

/// <summary>
/// DTO for the context information for suggesting story developments.
/// </summary>
public class SuggestStoryDevelopmentsContextInfoDto : NovelTextGenerationContextInfoDto
{
    private const string _storySuggestionsJsonSchema = """
                                                       {
                                                         "type": "array",
                                                         "items": {
                                                           "type": "object",
                                                           "properties": {
                                                             "title": { "type": "string" },
                                                             "description": { "type": "string" }
                                                           },
                                                           "required": ["title", "description"],
                                                           "additionalProperties": false
                                                         }
                                                       }
                                                       """;

    /// <summary>
    /// The index of the chapter.
    /// </summary>
    public int ChapterIndex { get; set; }

    /// <summary>
    /// The index of the section.
    /// </summary>
    public int SectionIndex { get; set; }

    /// <summary>
    /// The text offset of the current cursor location.
    /// </summary>
    public int TextOffset { get; set; }

    /// <inheritdoc />
    public override StructuredOutputOptions? GetStructuredOutputOptions() => new()
    {
        SchemaName = "story_development_suggestions",
        JsonSchema = _storySuggestionsJsonSchema,
        Strict = true
    };
}

/// <summary>
/// DTO for the context information for translating a novel.
/// </summary>
public class TranslateNovelContextInfoDto : NovelTextGenerationContextInfoDto
{
    private const string _translatedNovelJsonSchema = """
                                                      {
                                                        "type": "object",
                                                        "properties": {
                                                          "chapterTitle": { "type": "string" },
                                                          "storyEvents": {
                                                            "type": "array",
                                                            "items": {
                                                              "type": "object",
                                                              "properties": {
                                                                "title": { "type": "string" },
                                                                "date": { "type": "string" },
                                                                "description": { "type": "string" }
                                                              },
                                                              "required": ["title", "date", "description"],
                                                              "additionalProperties": false
                                                            }
                                                          },
                                                          "sections": {
                                                            "type": "array",
                                                            "items": {
                                                              "type": "object",
                                                              "properties": {
                                                                "sectionIndex": { "type": "integer" },
                                                                "summary": { "type": "string" },
                                                                "text": { "type": "string" }
                                                              },
                                                              "required": ["sectionIndex", "summary", "text"],
                                                              "additionalProperties": false
                                                            }
                                                          }
                                                        },
                                                        "required": ["chapterTitle", "storyEvents", "sections"],
                                                        "additionalProperties": false
                                                      }
                                                      """;

    /// <summary>
    /// The chapter index to translate from.
    /// </summary>
    public int ChapterIndex { get; set; }

    /// <summary>
    /// The target language of the translation.
    /// </summary>
    public WritingLanguage TargetLanguage { get; set; }

    /// <summary>
    /// Optional user instructions for the translation.
    /// </summary>
    public string? Instructions { get; set; }

    /// <inheritdoc />
    public override StructuredOutputOptions? GetStructuredOutputOptions() => new()
    {
        SchemaName = "translated_novel_sections",
        JsonSchema = _translatedNovelJsonSchema,
        Strict = true
    };
}
