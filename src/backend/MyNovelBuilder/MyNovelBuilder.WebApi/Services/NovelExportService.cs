using System.Text;
using Markdig;
using MyNovelBuilder.WebApi.Extensions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Service for exporting novels.
/// </summary>
public partial class NovelExportService : INovelExportService
{
    private readonly INovelService _novelService;

    static NovelExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary></summary>
    public NovelExportService(INovelService novelService)
    {
        _novelService = novelService;
    }

    /// <inheritdoc />
    public async Task<string> ExportToMarkdownAsync(Guid novelId, CancellationToken cancellationToken = default)
    {
        var novel = await _novelService.GetByIdAsync(novelId, cancellationToken);
        var prose = await _novelService.GetProseAsync(novelId, cancellationToken);

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

    /// <inheritdoc />
    public async Task<string> ExportToHtmlAsync(Guid novelId, CancellationToken cancellationToken = default)
    {
        var markdown = await ExportToMarkdownAsync(novelId, cancellationToken);
        return Markdown.ToHtml(markdown);
    }

    /// <inheritdoc />
    public async Task<byte[]> ExportToPdfAsync(Guid novelId, CancellationToken cancellationToken = default)
    {
        var novel = await _novelService.GetByIdAsync(novelId, cancellationToken);
        var prose = await _novelService.GetProseAsync(novelId, cancellationToken);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(20, Unit.Millimetre);
                page.DefaultTextStyle(x => x.FontFamily(Fonts.TimesNewRoman).FontSize(12).LineHeight(1.5f));

                page.Content()
                    .Column(column =>
                    {
                        column.Item().Text(novel.Title.StripHtml()).FontSize(24).Bold();

                        if (!string.IsNullOrWhiteSpace(novel.Author))
                        {
                            column.Item().Text($"by {novel.Author}").FontSize(14).Italic().FontColor(Colors.Grey.Darken2);
                        }

                        if (!string.IsNullOrWhiteSpace(novel.Brief))
                        {
                            column.Item().PaddingTop(10).Text(novel.Brief.StripHtml());
                        }

                        foreach (var chapter in prose.Chapters)
                        {
                            column.Item().PaddingTop(16).Text(chapter.Title.StripHtml()).FontSize(18).Bold();

                            foreach (var section in chapter.Sections)
                            {
                                var text = section.Text.StripHtml().Trim();
                                if (string.IsNullOrWhiteSpace(text))
                                {
                                    continue;
                                }

                                column.Item().PaddingTop(8).Text(text);
                            }
                        }
                    });
            });
        }).GeneratePdf();
    }
}
