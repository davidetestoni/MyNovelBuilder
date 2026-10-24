using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Helpers;

/// <summary>Lossless, versioned splitting for finite audiobook synthesis.</summary>
public static class AudiobookTextSplitter
{
    /// <summary>Bump when boundaries or provider budgets change.</summary>
    public const int Version = 1;

    /// <summary>Conservative request budgets, matching existing local provider chunk limits.</summary>
    public static int Limit(TtsProvider provider) => provider switch
    {
        TtsProvider.Chatterbox or TtsProvider.Qwen3 or TtsProvider.OmniVoice or
        TtsProvider.Audio8 or TtsProvider.KittenTts or TtsProvider.DeApi => 500,
        _ => 1000
    };

    /// <summary>Preserves every UTF-16 character, including whitespace, without splitting surrogate pairs.</summary>
    public static IReadOnlyList<string> Split(string text, int limit, bool preserveTags = false)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 2);
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
                if (end < text.Length && char.IsHighSurrogate(text[end - 1]) && char.IsLowSurrogate(text[end])) end--;
                if (end < text.Length && text[end - 1] == '\r' && text[end] == '\n') end--;
            }
            result.Add(text[start..end]);
            start = end;
        }
        return result;
    }
}
