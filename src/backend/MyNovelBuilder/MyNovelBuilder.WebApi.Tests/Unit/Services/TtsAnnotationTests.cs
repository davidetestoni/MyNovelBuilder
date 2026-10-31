using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Services;
using MyNovelBuilder.WebApi.Models.Prompts;
using MyNovelBuilder.WebApi.Models.TextGeneration;
using MyNovelBuilder.WebApi.Services.TextGeneration;

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
        Assert.Equal("source", fish.Messages("source")[^1].Message);
    }
    [Theory]
    [InlineData("\"Stay here.\"", "\"[whispers]Stay here.\"", "\"[whispers]Stay here.\"")]
    [InlineData("\"Stay here.\"", "[whispers]Stay here.", "\"[whispers]Stay here.\"")]
    [InlineData("\"Stay here.\"", "\"[whispers]Stay here.", "\"[whispers]Stay here.\"")]
    [InlineData("\"Stay here.\"", "[whispers]Stay here.\"", "\"[whispers]Stay here.\"")]
    [InlineData("“Stay here.”", "[whispers]Stay here.", "“[whispers]Stay here.”")]
    [InlineData("\"He said \"stay\".\"", "[soft]He said \"stay\".", "\"[soft]He said \"stay\".\"")]
    public async Task QuotedDialoguePreservesOrRestoresOnlyBoundaryQuotes(string source, string response, string expected)
    {
        var generator = new AnnotationGenerator(response);
        var result = await TtsAnnotationService.AnnotateAsync(source,
            TtsAnnotationProfile.Resolve(TtsProvider.OpenRouter, "fish-audio/drama-3"),
            generator, "model", CancellationToken.None);
        Assert.Equal(expected, result);
        Assert.True(TtsAnnotationService.PreservesSource(source, result));
        Assert.Single(generator.Prompts);
    }

    [Theory]
    [InlineData("[soft]Changed words.")]
    [InlineData("[unclosed Stay here.")]
    [InlineData("[soft]Stay  here.")]
    public async Task InvalidAnnotationRetriesWithCorrectionThenUsesOriginalText(string response)
    {
        const string source = "\"Stay here.\"";
        var generator = new AnnotationGenerator(response);
        var result = await TtsAnnotationService.AnnotateAsync(source,
            TtsAnnotationProfile.Resolve(TtsProvider.ElevenLabs, "eleven_v3"),
            generator, "model", CancellationToken.None);
        Assert.Equal(source, result);
        Assert.Equal(2, generator.Prompts.Count);
        Assert.Contains("previous attempt failed validation", generator.Prompts[1]);
    }

    [Fact]
    public async Task CancellationDoesNotBecomeUnannotatedFallback()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var generator = new AnnotationGenerator("invalid");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => TtsAnnotationService.AnnotateAsync("source",
            TtsAnnotationProfile.Resolve(TtsProvider.ElevenLabs, "eleven_v3"), generator, "model", cancellation.Token));
        Assert.Empty(generator.Prompts);
    }

    private sealed class AnnotationGenerator(string response) : ITextGenerationService
    {
        public List<string> Prompts { get; } = [];
        public Task<string> GenerateAsync(string model, IEnumerable<PromptMessage> messages,
            StructuredOutputOptions? structuredOutputOptions = null, CancellationToken cancellationToken = default)
        {
            Prompts.Add(messages.First().Message);
            return Task.FromResult(response);
        }
        public IAsyncEnumerable<string> GenerateStreamedAsync(string model, IEnumerable<PromptMessage> messages, StructuredOutputOptions? structuredOutputOptions = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> DescribeImageAsync(string model, IEnumerable<PromptMessage> messages, byte[] imageBytes, string imageMimeType, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<TextGenerationModelInfo>> GetAvailableModelsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

}
