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

    /// <summary>
    /// Exports a novel to HTML.
    /// </summary>
    Task<string> ExportToHtmlAsync(Guid novelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports a novel to PDF.
    /// </summary>
    Task<byte[]> ExportToPdfAsync(Guid novelId, CancellationToken cancellationToken = default);
}
