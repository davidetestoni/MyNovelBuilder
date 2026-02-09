using System.Text;
using System.Text.RegularExpressions;

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
        sb.AppendLine($"# {StripHtml(novel.Title)}");
        
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
            sb.AppendLine($"## {StripHtml(chapter.Title)}");
            foreach (var section in chapter.Sections)
            {
                var text = StripHtml(section.Text);

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

    private static string StripHtml(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        // Replace </p><p> with newline and strip other HTML tags
        var stripped = StripHtmlRegex().Replace(
            input.Replace("\u003C/p\u003E\u003Cp\u003E", Environment.NewLine),
            string.Empty);
        
        var decoded = System.Net.WebUtility.HtmlDecode(stripped);
    
        // Replace non-breaking spaces (0xA0) with regular spaces (0x20)
        return decoded.Replace('\u00A0', ' ');
    }

    [GeneratedRegex("<.*?>")]
    private static partial Regex StripHtmlRegex();
}
