using System.Text.RegularExpressions;
using Markdig;
using MyNovelBuilder.WebApi.Exceptions;
using MyNovelBuilder.WebApi.Extensions;
using MyNovelBuilder.WebApi.Models.Novels;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Parses novels from supported import formats.
/// </summary>
public partial class NovelImportService : INovelImportService
{
    /// <inheritdoc />
    public Prose ImportFromMarkdown(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            throw InvalidMarkdown("The Markdown file is empty.");
        }

        var lines = markdown.TrimStart('\uFEFF').Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var headings = FindDocumentHeadings(lines);
        var titleHeading = headings.FirstOrDefault(x => x.Level == 1);

        if (titleHeading is null)
        {
            throw InvalidMarkdown("The Markdown file must contain a level-one title, for example '# My Novel'.");
        }

        var title = ToPlainText(titleHeading.Text);
        ValidateRequired(title, "title");
        ValidateLength(title, 100, "title");

        var chapterHeadings = headings
            .Where(x => x.Level == 2 && x.LineIndex > titleHeading.LineIndex)
            .ToList();
        var chapters = new List<Chapter>();
        for (var i = 0; i < chapterHeadings.Count; i++)
        {
            var heading = chapterHeadings[i];
            var chapterTitle = ToPlainText(heading.Text);
            ValidateRequired(chapterTitle, "chapter title");
            ValidateLength(chapterTitle, 200, "chapter title");

            var bodyEnd = i + 1 < chapterHeadings.Count
                ? chapterHeadings[i + 1].LineIndex
                : lines.Length;
            var body = string.Join('\n', lines[(heading.LineIndex + 1)..bodyEnd]).Trim();
            var sections = string.IsNullOrWhiteSpace(body)
                ? Array.Empty<Section>()
                : new[]
                {
                    new Section
                    {
                        Text = Markdown.ToHtml(body).Trim()
                    }
                };

            chapters.Add(new Chapter
            {
                Title = chapterTitle,
                Sections = sections
            });
        }

        return new Prose { Chapters = chapters };
    }

    private static List<MarkdownHeading> FindDocumentHeadings(string[] lines)
    {
        var headings = new List<MarkdownHeading>();
        string? fence = null;

        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimStart();
            var fenceMatch = FenceRegex().Match(trimmed);
            if (fenceMatch.Success)
            {
                var marker = fenceMatch.Groups[1].Value;
                if (fence is null)
                {
                    fence = marker;
                }
                else if (marker[0] == fence[0] && marker.Length >= fence.Length)
                {
                    fence = null;
                }

                continue;
            }

            if (fence is not null)
            {
                continue;
            }

            var match = HeadingRegex().Match(lines[i]);
            if (!match.Success)
            {
                continue;
            }

            headings.Add(new MarkdownHeading(
                i,
                match.Groups[1].Value.Length,
                match.Groups[2].Value.Trim()));
        }

        return headings;
    }

    private static string ToPlainText(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        return Markdown.ToHtml(markdown)
            .Replace("</p>\n<p>", Environment.NewLine, StringComparison.Ordinal)
            .StripHtml()
            .Trim();
    }

    private static void ValidateLength(string value, int maximumLength, string fieldName)
    {
        if (value.Length > maximumLength)
        {
            throw InvalidMarkdown($"The imported {fieldName} cannot exceed {maximumLength} characters.");
        }
    }

    private static void ValidateRequired(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw InvalidMarkdown($"The imported {fieldName} cannot be empty.");
        }
    }

    private static ApiException InvalidMarkdown(string message) =>
        new(ErrorCodes.InvalidFile, message);

    private sealed record MarkdownHeading(int LineIndex, int Level, string Text);

    [GeneratedRegex("^(#{1,2})[\\t ]+(.+?)[\\t ]*#*[\\t ]*$")]
    private static partial Regex HeadingRegex();

    [GeneratedRegex("^(`{3,}|~{3,})")]
    private static partial Regex FenceRegex();
}
