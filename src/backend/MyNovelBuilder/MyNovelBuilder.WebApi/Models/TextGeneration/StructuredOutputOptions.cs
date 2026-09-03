namespace MyNovelBuilder.WebApi.Models.TextGeneration;

/// <summary>
/// Options for structured JSON output.
/// </summary>
public class StructuredOutputOptions
{
    /// <summary>
    /// The schema name.
    /// </summary>
    public required string SchemaName { get; init; }

    /// <summary>
    /// The JSON schema to enforce.
    /// </summary>
    public required string JsonSchema { get; init; }

    /// <summary>
    /// Whether the schema should be applied strictly.
    /// </summary>
    public bool Strict { get; init; } = true;
}
