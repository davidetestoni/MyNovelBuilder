namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for exporting novels.
/// </summary>
public interface INovelExportService
{
    /// <summary>
    /// Exports a novel to Markdown.
    /// </summary>
    Task<string> ExportToMarkdownAsync(Guid novelId, CancellationToken cancellationToken = default);
}
