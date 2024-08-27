namespace MyNovelBuilder.WebApi.Dtos.Generate;

/// <summary>
/// DTO for a chunk of streamed text.
/// </summary>
public class GenerateTextResponseChunkDto
{
    /// <summary>
    /// The content of the chunk.
    /// </summary>
    public required string Content { get; set; }
}
