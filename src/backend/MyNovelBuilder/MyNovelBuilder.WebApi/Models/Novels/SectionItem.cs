using System.Text.Json.Serialization;

namespace MyNovelBuilder.WebApi.Models.Novels;

/// <summary>
/// A section item.
/// </summary>
[JsonDerivedType(typeof(SectionItem), typeDiscriminator: "base")]
[JsonDerivedType(typeof(TextSectionItem), typeDiscriminator: "text")]
[JsonDerivedType(typeof(ImageSectionItem), typeDiscriminator: "image")]
public class SectionItem
{
    
}

/// <summary>
/// A text section item.
/// </summary>
public class TextSectionItem : SectionItem
{
    /// <summary>
    /// The text.
    /// </summary>
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// An image section item.
/// </summary>
public class ImageSectionItem : SectionItem
{
    /// <summary>
    /// The image ID.
    /// </summary>
    public Guid ImageId { get; set; }
}
