using System.Text.Json.Serialization;

namespace MyNovelBuilder.WebApi.Dtos.Generate;

/// <summary>
/// DTO for compendium-scoped text generation context information.
/// </summary>
[JsonDerivedType(typeof(DescribeImageContextInfoDto), typeDiscriminator: "describeImage")]
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
