using Microsoft.ML.Tokenizers;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Token counting service backed by Microsoft.ML.Tokenizers.
/// </summary>
public class TokenizerService : ITokenizerService
{
    private static readonly Lazy<Tokenizer> _lazyTokenizer = new(
        TiktokenTokenizer.CreateForModel("gpt-4o"));

    /// <inheritdoc />
    public int CountTokens(string text)
    {
        return _lazyTokenizer.Value.CountTokens(text);
    }
}
