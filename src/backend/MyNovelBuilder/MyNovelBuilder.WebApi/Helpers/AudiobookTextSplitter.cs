using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Helpers;

/// <summary>Lossless, versioned splitting for finite audiobook synthesis.</summary>
public static class AudiobookTextSplitter
{
    /// <summary>Bump when boundaries or provider budgets change.</summary>
    public const int Version = 2;

    /// <summary>Conservative request budgets, matching existing local provider chunk limits.</summary>
    public static int Limit(TtsProvider provider) => provider switch
    {
        TtsProvider.Chatterbox or TtsProvider.Qwen3 or TtsProvider.OmniVoice or
        TtsProvider.Audio8 or TtsProvider.KittenTts or TtsProvider.Ming or TtsProvider.DeApi => 500,
        _ => 1000
    };

    /// <summary>Preserves every UTF-16 character, including whitespace, without splitting surrogate pairs.</summary>
    public static IReadOnlyList<string> Split(string text, int limit, bool preserveTags = false)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 2);
        if (preserveTags && text.Contains("<whisper>")) return SplitScoped(text, limit);
        var result = new List<string>();
        var start = 0;
        while (start < text.Length)
        {
            var end = Math.Min(start + limit, text.Length);
            if (end < text.Length)
            {
                // Prefer paragraph, sentence, then word boundaries within this request budget.
                var boundary = -1;
                for (var priority = 0; priority < 3 && boundary < 0; priority++)
                {
                    for (var i = end - 1; i > start; i--)
                    {
                        if ((priority == 0 && text[i] == '\n') ||
                            (priority == 1 && text[i] is '.' or '!' or '?' && char.IsWhiteSpace(text[i + 1])) ||
                            (priority == 2 && char.IsWhiteSpace(text[i])))
                        { boundary = i + 1; break; }
                    }
                }
                if (boundary > start) end = boundary;
                if (preserveTags)
                {
                    var opening = text.LastIndexOf('[', end - 1, end - start);
                    if (opening >= start && text.IndexOf(']', opening) is var closing && closing >= end)
                    {
                        if (closing - opening + 1 > limit)
                            throw new InvalidDataException("An emphasis tag exceeds the provider request limit.");
                        end = opening == start ? closing + 1 : opening;
                    }
                }
                if (preserveTags && end < text.Length)
                {
                    var trailing = System.Text.RegularExpressions.Regex.Match(text[start..end], @"(?:\[[^\]]+\]\s*)+$");
                    if (trailing.Success)
                    {
                        if (trailing.Index > 0) end = start + trailing.Index;
                        else
                        {
                            var next = end;
                            while (next < text.Length && char.IsWhiteSpace(text[next])) next++;
                            if (next < text.Length) next += char.IsHighSurrogate(text[next]) ? 2 : 1;
                            if (next - start > limit)
                                throw new InvalidDataException("A delivery annotation leaves no room for speech in the synthesis budget.");
                            end = next;
                        }
                    }
                }
                if (end < text.Length && char.IsHighSurrogate(text[end - 1]) && char.IsLowSurrogate(text[end])) end--;
                if (end < text.Length && text[end - 1] == '\r' && text[end] == '\n') end--;
            }
            result.Add(text[start..end]);
            start = end;
        }
        return result;
    }
    private static IReadOnlyList<string> SplitScoped(string text, int limit)
    {
        // Keep each independent synthesis request balanced. Reserve closing markup in its budget.
        var chunks = new List<string>();
        var current = new System.Text.StringBuilder();
        var scoped = false;
        foreach (var token in System.Text.RegularExpressions.Regex.Matches(
            text, @"<whisper>|</whisper>|\[[^\]]*\]|[\uD800-\uDBFF][\uDC00-\uDFFF]|[\s\S]").Select(match => match.Value))
        {
            var nextScoped = token == "<whisper>" || (scoped && token != "</whisper>");
            var reserve = nextScoped ? 10 : 0;
            if (current.Length + token.Length + reserve > limit)
            {
                if (current.Length == 0 || current.ToString() == "<whisper>")
                    throw new InvalidDataException("A delivery annotation exceeds the synthesis budget.");
                chunks.Add(current + (scoped ? "</whisper>" : ""));
                current.Clear();
                if (scoped) current.Append("<whisper>");
                if (current.Length + token.Length + reserve > limit)
                    throw new InvalidDataException("A delivery annotation exceeds the synthesis budget.");
            }
            current.Append(token);
            scoped = nextScoped;
        }
        if (scoped) throw new InvalidDataException("Unclosed whisper direction.");
        if (current.Length > 0) chunks.Add(current.ToString());
        return chunks;
    }

}
