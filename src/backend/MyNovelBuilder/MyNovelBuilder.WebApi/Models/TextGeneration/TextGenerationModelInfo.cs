namespace MyNovelBuilder.WebApi.Models.TextGeneration;

/// <summary>
/// Information about a text generation model.
/// </summary>
public class TextGenerationModelInfo
{
    /// <summary>
    /// The model id.
    /// </summary>
    public required string Id { get; set; }
}