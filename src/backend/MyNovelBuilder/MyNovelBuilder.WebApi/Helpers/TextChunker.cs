namespace MyNovelBuilder.WebApi.Helpers;

/// <summary>
/// Helper class to chunk text into smaller pieces based on punctuation and max size.
/// </summary>
public class TextChunker
{
    private readonly int _maxChunkSize;
    
    // Punctuation marks that indicate hard breaks, ordered by preference
    private static readonly char[] _hardBreaks = ['.', '!', '?'];
    private static readonly char[] _quoteMarks = ['"', '\'', '“', '”', '‘', '’'];
    
    /// <summary></summary>
    public TextChunker(int maxChunkSize)
    {
        if (maxChunkSize <= 0)
        {
            throw new ArgumentException("Max chunk size must be greater than 0", nameof(maxChunkSize));
        }
            
        _maxChunkSize = maxChunkSize;
    }
    
    /// <summary>
    /// Chunks the input text into smaller pieces based on punctuation and max size.
    /// </summary>
    public List<string> ChunkText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }
            
        var chunks = new List<string>();
        var position = 0;
        
        while (position < text.Length)
        {
            var chunkEnd = FindChunkEnd(text, position);
            var chunk = text.Substring(position, chunkEnd - position).Trim();

            if (!string.IsNullOrWhiteSpace(chunk))
            {
                chunks.Add(chunk);
            }
                
            position = chunkEnd;
        }
        
        return chunks;
    }
    
    private int FindChunkEnd(string text, int start)
    {
        // If remaining text fits within max size, return the end
        var remainingLength = text.Length - start;
        if (remainingLength <= _maxChunkSize)
        {
            return text.Length;
        }
            
        // Find the ideal break point within maxChunkSize
        var searchEnd = Math.Min(start + _maxChunkSize, text.Length);
        
        // Try to find a break point, searching backwards from the max position
        var breakPoint = FindBestBreakPoint(text, start, searchEnd);

        if (breakPoint > start)
        {
            return breakPoint;
        }
            
        // If no good break point found, force split at maxChunkSize
        // but try to at least break at a space
        var spaceIndex = text.LastIndexOf(' ', searchEnd - 1, searchEnd - start);
        if (spaceIndex > start)
        {
            return spaceIndex + 1;
        }
            
        return searchEnd;
    }
    
    private static int FindBestBreakPoint(string text, int start, int searchEnd)
    {
        var bestBreak = -1;
        
        // Search backwards from the end of our search range
        for (var i = searchEnd - 1; i > start; i--)
        {
            var currentChar = text[i];
            
            // Check if this is a hard break character
            if (_hardBreaks.Contains(currentChar))
            {
                // Check if it's followed by whitespace or a quote (typical sentence end)
                if (IsValidSentenceEnd(text, i))
                {
                    return i + 1;
                }
                bestBreak = i + 1; // Store as potential break even if not ideal
            }
            
            // Check for closing quotes that might end dialogue
            if (!_quoteMarks.Contains(currentChar))
            {
                continue;
            }

            // If quote is followed by punctuation or whitespace, it's a good break
            if (i + 1 >= text.Length)
            {
                continue;
            }

            var nextChar = text[i + 1];
            if (!char.IsWhiteSpace(nextChar) && !_hardBreaks.Contains(nextChar))
            {
                continue;
            }

            // If we haven't found a sentence-ending punctuation yet, use this
            if (bestBreak == -1)
            {
                bestBreak = i + 1;
            }
        }
        
        return bestBreak > start ? bestBreak : -1;
    }
    
    private static bool IsValidSentenceEnd(string text, int punctuationIndex)
    {
        // Check what follows the punctuation
        var nextIndex = punctuationIndex + 1;
        
        if (nextIndex >= text.Length)
            return true; // End of text
            
        var nextChar = text[nextIndex];
        
        // Valid if followed by whitespace, quote, or end of text
        if (char.IsWhiteSpace(nextChar) || _quoteMarks.Contains(nextChar))
        {
            return true;
        }
            
        // Check for quote followed by whitespace (e.g., ." or !")
        if (nextIndex + 1 < text.Length && _quoteMarks.Contains(nextChar))
        {
            return char.IsWhiteSpace(text[nextIndex + 1]);
        }
        
        return false;
    }
}
