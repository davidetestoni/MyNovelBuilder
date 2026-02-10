using System.Text;
using MyNovelBuilder.WebApi.Extensions;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for exporting novels.
/// </summary>
public partial class NovelExportService : INovelExportService
{
    private readonly INovelService _novelService;

    /// <summary></summary>
    public NovelExportService(INovelService novelService)
    {
        _novelService = novelService;
    }

    /// <inheritdoc />
    public async Task<string> ExportToMarkdownAsync(Guid novelId)
    {
        var novel = await _novelService.GetByIdAsync(novelId);
        var prose = await _novelService.GetProseAsync(novelId);

        var sb = new StringBuilder();
        sb.AppendLine($"# {novel.Title.StripHtml()}");
        
        if (!string.IsNullOrWhiteSpace(novel.Author))
        {
            sb.AppendLine($"by {novel.Author}");
        }
        
        sb.AppendLine();
        
        if (!string.IsNullOrWhiteSpace(novel.Brief))
        {
            sb.AppendLine(novel.Brief);
            sb.AppendLine();
        }

        foreach (var chapter in prose.Chapters)
        {
            sb.AppendLine($"## {chapter.Title.StripHtml()}");
            foreach (var section in chapter.Sections)
            {
                var text = section.Text.StripHtml();

                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                sb.AppendLine(text);
                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd();
    }
}
