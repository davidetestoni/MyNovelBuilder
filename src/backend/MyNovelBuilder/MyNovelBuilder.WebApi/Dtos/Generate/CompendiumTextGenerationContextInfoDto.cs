using System.Text.Json.Serialization;

namespace MyNovelBuilder.WebApi.Dtos.Generate;

/// <summary>
/// DTO for compendium-scoped text generation context information.
/// </summary>
[JsonDerivedType(typeof(DescribeImageContextInfoDto), typeDiscriminator: "describeImage")]
[JsonDerivedType(typeof(CreateCompendiumRecordImageGenerationPromptContextInfoDto), typeDiscriminator: "createCompendiumRecordImageGenerationPrompt")]
public class CompendiumTextGenerationContextInfoDto : TextGenerationContextInfoDto
{
}

/// <summary>
/// DTO for the context information for image description.
/// </summary>
public class DescribeImageContextInfoDto : CompendiumTextGenerationContextInfoDto
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
