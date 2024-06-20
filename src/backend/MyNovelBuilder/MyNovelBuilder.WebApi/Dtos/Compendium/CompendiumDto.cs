namespace MyNovelBuilder.WebApi.Dtos.Compendium;

/// <summary>
/// Data transfer object for a compendium.
/// </summary>
public class CompendiumDto
{
    /// <summary>
    /// The compendium's name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The compendium's description.
    /// </summary>
    public required string Description { get; set; }
}
