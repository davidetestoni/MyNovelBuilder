namespace MyNovelBuilder.WebApi.Options;

/// <summary>
/// Controls one-time imports into a new application data directory.
/// </summary>
public sealed class SeedOptions
{
    /// <summary>
    /// The configuration section containing seed options.
    /// </summary>
    public const string SectionName = "Seed";

    /// <summary>
    /// Whether the bundled sample novel should be imported once.
    /// </summary>
    public bool IncludeSampleNovel { get; set; } = true;
}
