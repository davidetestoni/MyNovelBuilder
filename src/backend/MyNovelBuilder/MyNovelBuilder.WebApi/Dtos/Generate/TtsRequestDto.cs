namespace MyNovelBuilder.WebApi.Dtos.Generate;

/// <summary>
/// DTO for a Text-to-Speech request.
/// </summary>
public class TtsRequestDto
{
    /// <summary>
    /// The message to generate audio for.
    /// </summary>
    public required string Message { get; set; }
}
