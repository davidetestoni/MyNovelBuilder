using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Models.AudioGeneration;
using MyNovelBuilder.WebApi.Services;

namespace MyNovelBuilder.WebApi.Tests.Unit.Services;

public sealed class AudiobookCostEstimatorTests
{
    private static ResolvedAudiobookSettings Settings => new(TtsProvider.OpenRouter, "fish-audio/drama-3", "voice",
        false, false, 100, TextGenerationProvider.OpenRouter, "text/model", null, null, null);
    private static AudiobookSectionSnapshot Section(string text = "Hello world.", string context = "Story context") =>
        new(0, text, [new(PromptMessageRole.User, context + text)], []);
    private static JsonArray Rates(string model = "fish-audio/drama-3", string input = "0.000015", string output = "0") =>
        new(new JsonObject { ["id"] = model, ["pricing"] = new JsonObject { ["prompt"] = input, ["completion"] = output } },
            new JsonObject { ["id"] = "text/model", ["pricing"] = new JsonObject { ["prompt"] = "0.000001", ["completion"] = "0.000003" } });

    [Fact]
    public void CharacterPricingUsesUncachedTextAndCountsSpeechOnlyOnce()
    {
        var result = AudiobookCostEstimator.Calculate(Settings, [Section()], Rates());
        Assert.False(result.IsPartial);
        Assert.Equal(new AudiobookCostRange(12 * .000015m, 12 * .000015m), result.Total);
        Assert.All(result.Stages.Skip(1), s => Assert.Equal("disabled", s.Status));
        var immersive = AudiobookCostEstimator.Calculate(Settings with { EnableImmersive = true, EnableTextEmphasis = true }, [Section()], Rates());
        Assert.All(immersive.Stages, s => Assert.Equal("estimated", s.Status));
        Assert.Equal(result.Total!.Low, immersive.Stages[0].Cost!.Low);
        Assert.Equal(result.Total.High * 1.15m, immersive.Stages[0].Cost!.High);
        Assert.True(immersive.Total!.Low > result.Total.Low);
    }

    [Fact]
    public void RenderedStoryContextIncreasesPlanningCost()
    {
        var settings = Settings with { EnableImmersive = true };
        var small = AudiobookCostEstimator.Calculate(settings, [Section()], Rates());
        var large = AudiobookCostEstimator.Calculate(settings, [Section(context: new string('x', 10000))], Rates());
        Assert.Equal(small.Stages[0], large.Stages[0]);
        Assert.True(large.Stages[2].Cost!.Low > small.Stages[2].Cost!.Low);
    }

    [Theory]
    [InlineData("bytedance-seed/seed-audio-1-0", "0", "0.0025", 1)]
    [InlineData("google/gemini-3.8-flash-tts", "0", "0.000009", 25)]
    public void DurationPricingUsesCorrectSecondsOrAudioTokenUnits(string model, string input, string output, int tokensPerSecond)
    {
        var passage = string.Join(' ', Enumerable.Repeat("word", 150));
        var result = AudiobookCostEstimator.Calculate(Settings with { ModelId = model }, [Section(passage)], Rates(model, input, output));
        var rate = decimal.Parse(output, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(50 * tokensPerSecond * rate, result.Total!.Low);
        Assert.Equal(75 * tokensPerSecond * rate, result.Total.High);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"prompt\":\"-1\",\"completion\":\"0\"}")]
    [InlineData("{\"prompt\":\"bad\",\"completion\":\"0\"}")]
    [InlineData("{\"prompt\":\"0\",\"completion\":\"0.001\"}")]
    public void UnknownOrInvalidSpeechPricingProducesPartialTotal(string pricing)
    {
        var rates = Rates();
        rates[0]!["pricing"] = JsonNode.Parse(pricing);
        var result = AudiobookCostEstimator.Calculate(Settings with { EnableImmersive = true }, [Section()], rates);
        Assert.True(result.IsPartial);
        Assert.Null(result.Stages[0].Cost);
        Assert.Equal(result.Stages[2].Cost, result.Total);
    }

    [Fact]
    public void UnknownProviderIsNotFreeButExplicitZeroRatesAre()
    {
        var unknown = AudiobookCostEstimator.Calculate(Settings with { Provider = TtsProvider.ElevenLabs }, [Section()], Rates());
        Assert.Null(unknown.Total);
        Assert.True(unknown.IsPartial);
        var free = AudiobookCostEstimator.Calculate(Settings, [Section()], Rates(input: "0"));
        Assert.Equal(new AudiobookCostRange(0, 0), free.Total);
        Assert.False(free.IsPartial);
    }

    [Fact]
    public async Task CachedSectionsNeedNoPricingLookupAndNoNewCharges()
    {
        using var handler = new CatalogueHandler();
        using var http = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var estimator = new AudiobookCostEstimator(http, cache, NullLogger<AudiobookCostEstimator>.Instance);
        var result = await estimator.EstimateAsync(Settings with { EnableImmersive = true, EnableTextEmphasis = true }, [], default);
        Assert.Equal(0, handler.Calls);
        Assert.False(result.IsPartial);
        Assert.Equal(new AudiobookCostRange(0, 0), result.Total);
    }

    [Fact]
    public async Task CatalogueIsCachedAndOnlyPublicMetadataIsRequested()
    {
        using var handler = new CatalogueHandler();
        using var http = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var estimator = new AudiobookCostEstimator(http, cache, NullLogger<AudiobookCostEstimator>.Instance);
        var first = await estimator.EstimateAsync(Settings, [Section()], default);
        var second = await estimator.EstimateAsync(Settings, [Section()], default);
        Assert.Equal(1, handler.Calls);
        Assert.NotNull(first.PricingAsOf);
        Assert.Equal(first.Total, second.Total);
    }

    [Fact]
    public async Task CatalogueOutageStillReturnsPreviewWithUnavailablePricing()
    {
        using var handler = new CatalogueHandler { Fail = true };
        using var http = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var estimator = new AudiobookCostEstimator(http, cache, NullLogger<AudiobookCostEstimator>.Instance);
        var result = await estimator.EstimateAsync(Settings, [Section()], default);
        Assert.True(result.IsPartial);
        Assert.Null(result.Total);
    }

    private sealed class CatalogueHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public bool Fail { get; init; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("openrouter.ai", request.RequestUri!.Host);
            Assert.Equal("/api/v1/models", request.RequestUri.AbsolutePath);
            Assert.Null(request.Content);
            if (Fail) throw new HttpRequestException("Offline");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(new JsonObject { ["data"] = Rates() }.ToJsonString())
            });
        }
    }
}
