using System.Text;
using Microsoft.AspNetCore.Mvc;
using MyNovelBuilder.WebApi.Extensions;
using MyNovelBuilder.WebApi.Services;

namespace MyNovelBuilder.WebApi.Controllers;

/// <summary>
/// Controller for exporting novels.
/// </summary>
[ApiController]
[Route("api/novel/{novelId:guid}/export")]
public class NovelExportController : ControllerBase
{
    private readonly INovelExportService _novelExportService;
    private readonly INovelService _novelService;

    /// <summary></summary>
    public NovelExportController(INovelExportService novelExportService, INovelService novelService)
    {
        _novelExportService = novelExportService;
        _novelService = novelService;
    }

    /// <summary>
    /// Exports a novel to Markdown.
    /// </summary>
    [HttpGet("markdown")]
    public async Task<IActionResult> ExportToMarkdown(Guid novelId, CancellationToken cancellationToken = default)
    {
        var novel = await _novelService.GetByIdAsync(novelId, cancellationToken);
        var markdown = await _novelExportService.ExportToMarkdownAsync(novelId, cancellationToken);
        var fileName = $"{novel.Title.SanitizeFileName().ToLower().Replace(" ", "_")}.md";
        return File(Encoding.UTF8.GetBytes(markdown), "text/markdown", fileName);
    }
}
