using MyNovelBuilder.WebApi.Models.Novels;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Parses novels from supported import formats.
/// </summary>
public interface INovelImportService
{
    /// <summary>
    /// Parses a novel from the Markdown format produced by the novel exporter.
    /// </summary>
    Prose ImportFromMarkdown(string markdown);
}
