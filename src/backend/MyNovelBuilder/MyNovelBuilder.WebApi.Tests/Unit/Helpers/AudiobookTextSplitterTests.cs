using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Helpers;
namespace MyNovelBuilder.WebApi.Tests.Unit.Helpers;

public sealed class AudiobookTextSplitterTests
{
    [Theory]
    [InlineData("Paragraph one.\r\n\r\nParagraph two.\nA third! \"Words\" 🌟 end.")]
    [InlineData("🌟🌟🌟🌟🌟🌟🌟🌟🌟🌟")]
    [InlineData("averylongwordwithoutanybreaks")]
    [InlineData(" \r\n\r\n\n        \t  ")]
    public void SplittingPreservesEveryCharacterAndRespectsLimits(string text)
    {
        for (var limit = 2; limit <= 20; limit++)
        {
            var chunks = AudiobookTextSplitter.Split(text, limit);
            Assert.Equal(text, string.Concat(chunks));
            Assert.Equal(chunks, AudiobookTextSplitter.Split(text, limit));
            Assert.All(chunks, chunk =>
            {
                Assert.InRange(chunk.Length, 1, limit);
                Assert.False(char.IsLowSurrogate(chunk[0]));
                Assert.False(char.IsHighSurrogate(chunk[^1]));
            });
        }
    }

    [Fact]
    public void TagsStayWholeAndProviderBudgetsMatchLocalChunking()
    {
        const string text = "Speech [warm voice] more speech.";
        var chunks = AudiobookTextSplitter.Split(text, 15, preserveTags: true);
        Assert.Equal(text, string.Concat(chunks));
        Assert.All(chunks, chunk => Assert.Equal(chunk.Count(c => c == '['), chunk.Count(c => c == ']')));
        Assert.Equal(500, AudiobookTextSplitter.Limit(TtsProvider.OmniVoice));
        Assert.Equal(500, AudiobookTextSplitter.Limit(TtsProvider.KittenTts));
        Assert.Equal(1000, AudiobookTextSplitter.Limit(TtsProvider.UnrealSpeech));
    }
}
