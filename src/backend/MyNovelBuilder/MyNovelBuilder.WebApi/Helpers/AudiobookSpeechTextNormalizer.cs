using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace MyNovelBuilder.WebApi.Helpers;

/// <summary>Versioned conversion of editor HTML into spoken text.</summary>
public static partial class AudiobookSpeechTextNormalizer
{
    public const int Version = 2;
    private static readonly HashSet<string> NonSpokenElements = new(StringComparer.OrdinalIgnoreCase)
        { "script", "style", "template", "svg", "noscript" };
    private static readonly HashSet<string> ParagraphElements = new(StringComparer.OrdinalIgnoreCase)
        { "p", "div", "section", "article", "h1", "h2", "h3", "h4", "h5", "h6", "li", "blockquote", "tr" };

    public static string Normalize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        var document = new HtmlDocument();
        document.LoadHtml(html);
        var result = new StringBuilder();
        AppendNode(result, document.DocumentNode);
        var lines = result.ToString().Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n').Select(line => Whitespace().Replace(line.Trim(), " "));
        return ExcessNewlines().Replace(string.Join('\n', lines).Trim(), "\n\n");
    }

    private static void AppendNode(StringBuilder result, HtmlNode node)
    {
        if (node.NodeType == HtmlNodeType.Comment || NonSpokenElements.Contains(node.Name)) return;
        if (node is HtmlTextNode text)
        {
            result.Append(WebUtility.HtmlDecode(text.Text).Replace('\u00a0', ' '));
            return;
        }
        if (node.Name.Equals("br", StringComparison.OrdinalIgnoreCase))
        {
            result.Append('\n');
            return;
        }
        var paragraph = ParagraphElements.Contains(node.Name);
        if (paragraph) result.Append("\n\n");
        foreach (var child in node.ChildNodes) AppendNode(result, child);
        if (paragraph) result.Append("\n\n");
    }

    [GeneratedRegex(@"[\t\f\v ]+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExcessNewlines();
}
