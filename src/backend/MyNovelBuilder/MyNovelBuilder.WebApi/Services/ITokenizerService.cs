namespace MyNovelBuilder.WebApi.Services;

/// <summary>
/// Computes token counts.
/// </summary>
public interface ITokenizerService
{
    /// <summary>
    /// Count tokens in the provided text.
    /// </summary>
    int CountTokens(string text);
}
