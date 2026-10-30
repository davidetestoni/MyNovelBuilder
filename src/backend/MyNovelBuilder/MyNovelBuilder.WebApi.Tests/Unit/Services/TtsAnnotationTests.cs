using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Services;

namespace MyNovelBuilder.WebApi.Tests.Unit.Services;

public sealed class TtsAnnotationTests
{
    [Theory]
    [InlineData("[note] Hello.", "[soft][note] Hello.", true)]
    [InlineData("Hello.", "[whispers]Hello.", true)]
    [InlineData("Literal [note] <3.", "Literal [note] [soft]<3.", true)]
    [InlineData("Ciao 🌟!\nCome stai?", "[excited]Ciao 🌟!\nCome stai?", true)]
    [InlineData("Original.", "Rewritten.", false)]
    [InlineData("Original.", "[unclosed Original.", false)]
    [InlineData("Original.", "[soft]Original. Extra.", false)]
    public void ValidationAllowsOnlyInsertedDirections(string source, string annotated, bool valid) =>
        Assert.Equal(valid, TtsAnnotationService.PreservesSource(source, annotated));

    [Fact]
    public void ScopedDirectionsRemainBalancedAcrossSynthesisRequests()
    {
        var source = string.Concat(Enumerable.Repeat("A whispered sentence 🌟. ", 30));
        var text = "<whisper>" + source + "</whisper>";
        var chunks = MyNovelBuilder.WebApi.Helpers.AudiobookTextSplitter.Split(text, 80, preserveTags: true);
        Assert.True(chunks.Count > 1);
        Assert.All(chunks, chunk =>
        {
            Assert.InRange(chunk.Length, 1, 80);
            Assert.StartsWith("<whisper>", chunk);
            Assert.EndsWith("</whisper>", chunk);
        });
        Assert.Equal(source, string.Concat(chunks).Replace("<whisper>", "").Replace("</whisper>", ""));
        Assert.True(TtsAnnotationService.PreservesSource(source, text));
    }

    [Fact]
    public void ProfilesUseExactProviderModelPairsAndFewShotExamples()
    {
        var eleven = TtsAnnotationProfile.Resolve(TtsProvider.ElevenLabs, "eleven_v3");
        var fish = TtsAnnotationProfile.Resolve(TtsProvider.OpenRouter, "fish-audio/drama-3");
        Assert.NotNull(eleven);
        Assert.NotNull(fish);
        Assert.NotEqual(eleven.Directions, fish.Directions);
        Assert.Null(TtsAnnotationProfile.Resolve(TtsProvider.ElevenLabs, "eleven_multilingual_v2"));
        Assert.Null(TtsAnnotationProfile.Resolve(TtsProvider.Custom, "fish-audio/drama-3"));
        Assert.True(fish.Messages("source").Count >= 8);
        Assert.Equal("source", fish.Messages("source").Last().Message);
    }
}
