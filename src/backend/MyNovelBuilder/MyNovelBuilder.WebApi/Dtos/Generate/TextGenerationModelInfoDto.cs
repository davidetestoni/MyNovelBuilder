namespace MyNovelBuilder.WebApi.Dtos.Generate;

/// <summary>
/// DTO containing information about a text generation model.
/// </summary>
public class TextGenerationModelInfoDto
{
    /// <summary>
    /// The model id.
    /// </summary>
    public required string Id { get; set; }
}