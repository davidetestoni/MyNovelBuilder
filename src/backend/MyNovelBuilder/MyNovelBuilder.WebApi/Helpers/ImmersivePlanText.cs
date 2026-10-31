using System.Globalization;
using System.Text;

namespace MyNovelBuilder.WebApi.Helpers;

/// <summary>Conservative alignment of planner typography back to the exact source characters.</summary>
internal static class ImmersivePlanText
{
    internal static bool TryRestoreTypography(string source, IReadOnlyList<string> chunks, out string[] restored)
    {
        restored = [];
        var (expected, boundaries) = Canonicalize(source);
        var planned = chunks.Select(c => Canonicalize(c).Text).ToArray();
        if (expected != string.Concat(planned)) return false;
        var result = new List<string>();
        var position = 0;
        var sourceStart = 0;
        foreach (var chunk in planned)
        {
            position += chunk.Length;
            // Never split a source grapheme, ellipsis, or delivery tag between speakers.
            if (!boundaries.TryGetValue(position, out var sourceEnd)) return false;
            result.Add(source[sourceStart..sourceEnd]);
            sourceStart = sourceEnd;
        }
        if (sourceStart != source.Length) return false;
        restored = result.ToArray();
        return true;
    }

    private static (string Text, Dictionary<int, int> Boundaries) Canonicalize(string text)
    {
        var result = new StringBuilder();
        var boundaries = new Dictionary<int, int> { [0] = 0 };
        var offset = 0;
        while (offset < text.Length)
        {
            var tagEnd = text[offset] == '[' ? text.IndexOf(']', offset + 1) : -1;
            if (tagEnd >= 0)
            {
                // Tags are opaque: typography repair must never change a delivery instruction.
                result.Append(text.AsSpan(offset, tagEnd + 1 - offset));
                offset = tagEnd + 1;
            }
            else
            {
                var element = StringInfo.GetNextTextElement(text, offset);
                offset += element.Length;
                if (!string.IsNullOrWhiteSpace(element))
                    result.Append(element.Normalize(NormalizationForm.FormC)
                        .Replace('“', '"').Replace('”', '"').Replace('‘', '\'').Replace('’', '\'').Replace("…", "..."));
            }
            boundaries[result.Length] = offset;
        }
        return (result.ToString(), boundaries);
    }

    internal static string DescribeDifference(string source, string planned)
    {
        var expected = string.Concat(source.Where(c => !char.IsWhiteSpace(c)));
        var actual = string.Concat(planned.Where(c => !char.IsWhiteSpace(c)));
        var position = 0;
        while (position < Math.Min(expected.Length, actual.Length) && expected[position] == actual[position]) position++;
        static string CodeAt(string text, int offset) => offset < text.Length ? $"U+{(int)text[offset]:X4}" : "end of text";
        return $"Source text or annotations changed at non-whitespace character {position + 1}: "
            + $"expected {CodeAt(expected, position)}, received {CodeAt(actual, position)} "
            + $"(source length {expected.Length}, planned length {actual.Length}). "
            + $"Source excerpt: «{Excerpt(source, position)}»; planned excerpt: «{Excerpt(planned, position)}».";
    }

    private static string Excerpt(string text, int position)
    {
        var offset = 0;
        var count = 0;
        while (offset < text.Length && count < position)
        {
            if (!char.IsWhiteSpace(text[offset])) count++;
            offset++;
        }
        while (offset < text.Length && char.IsWhiteSpace(text[offset])) offset++;
        var start = Math.Max(0, offset - 40);
        var end = Math.Min(text.Length, offset + 40);
        var excerpt = (start > 0 ? "…" : "") + text[start..offset] + "⟦HERE⟧" + text[offset..end] + (end < text.Length ? "…" : "");
        return excerpt.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
    }
}
