using MyNovelBuilder.WebApi.Helpers;

namespace MyNovelBuilder.WebApi.Tests.Unit.Helpers;

public class TextChunkerTests
{
    [Fact]
    public void Constructor_WhenMaxChunkSizeIsZeroOrLess_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new TextChunker(0));
        Assert.Throws<ArgumentException>(() => new TextChunker(-1));
    }

    [Fact]
    public void ChunkText_WhenInputIsNullOrEmpty_ReturnsEmptyList()
    {
        var chunker = new TextChunker(100);
        
        Assert.Empty(chunker.ChunkText(null!));
        Assert.Empty(chunker.ChunkText(string.Empty));
        Assert.Empty(chunker.ChunkText("   "));
    }

    [Fact]
    public void ChunkText_WhenTextIsSmallerThanMaxSize_ReturnsSingleChunk()
    {
        const string text = "This is a short sentence.";
        var chunker = new TextChunker(100);

        var result = chunker.ChunkText(text);

        Assert.Single(result);
        Assert.Equal(text, result[0]);
    }

    [Fact]
    public void ChunkText_SplitsAtHardBreaks()
    {
        const string text = "First sentence. Second sentence! Third sentence?";
        
        // "First sentence. " is 16 chars. 
        // We set limit to 20 to ensure it breaks at the first period.
        var chunker = new TextChunker(20);

        var result = chunker.ChunkText(text);

        Assert.Equal(3, result.Count);
        Assert.Equal("First sentence.", result[0]);
        Assert.Equal("Second sentence!", result[1]);
        Assert.Equal("Third sentence?", result[2]);
    }

    [Fact]
    public void ChunkText_SplitsAtQuotesAfterPunctuation()
    {
        const string text = "\"I am here,\" he said. \"Are you?\" She nodded.";
        
        // ""I am here," he said. " is 22 chars.
        // ""Are you?" She nodded." is 22 chars.
        var chunker = new TextChunker(25);

        var result = chunker.ChunkText(text);

        Assert.Equal(2, result.Count);
        Assert.Equal("\"I am here,\" he said.", result[0]);
        Assert.Equal("\"Are you?\" She nodded.", result[1]);
    }

    [Fact]
    public void ChunkText_FallsBackToSpace_WhenNoHardBreakFound()
    {
        const string text = "This is a long sentence without any punctuation breaks here";
        
        // "This is a long " is 15 chars.
        var chunker = new TextChunker(15);

        var result = chunker.ChunkText(text);

        Assert.True(result.Count > 1);
        Assert.Equal("This is a long", result[0]);
    }

    [Fact]
    public void ChunkText_ForcesSplit_WhenNoSpaceFound()
    {
        const string text = "Supercalifragilisticexpialidocious";
        var chunker = new TextChunker(10);

        var result = chunker.ChunkText(text);

        Assert.Equal(4, result.Count);
        Assert.Equal("Supercalif", result[0]);
        Assert.Equal("ragilistic", result[1]);
        Assert.Equal("expialidoc", result[2]);
        Assert.Equal("ious", result[3]);
    }

    [Fact]
    public void ChunkText_HandlesSmartQuotes()
    {
        const string text = "“Hello!” she whispered. “Goodbye.”";
        
        // "“Hello!” " is 9 chars.
        // "she whispered. " is 15 chars.
        // “"Goodbye."” is 10 chars.
        var chunker = new TextChunker(20);

        var result = chunker.ChunkText(text);

        // First chunk will be "“Hello!”" (9 chars)
        // Next chunk starts at "she whispered. “Goodbye.”" (25 chars)
        // It will break at "she whispered. " (15 chars) and then the
        // last chunk will be "“Goodbye.”" (10 chars)
        Assert.Equal(3, result.Count);
        Assert.Equal("“Hello!”", result[0]);
        Assert.Equal("she whispered.", result[1]);
        Assert.Equal("“Goodbye.”", result[2]);
    }
}
