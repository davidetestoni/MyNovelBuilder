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
    
    /// <summary>
    /// Whether the model can accept image input.
    /// </summary>
    public bool IsVisionCapable { get; set; }
    
    /// <summary>
    /// Whether the model supports structured outputs (JSON schema response format).
    /// </summary>
    public bool SupportsStructuredOutputs { get; set; }
    
    /// <summary>
    /// The price per input token, in USD.
    /// </summary>
    public decimal InputTokenPrice { get; set; }
    
    /// <summary>
    /// The price per output token, in USD.
    /// </summary>
    public decimal OutputTokenPrice { get; set; }
}
