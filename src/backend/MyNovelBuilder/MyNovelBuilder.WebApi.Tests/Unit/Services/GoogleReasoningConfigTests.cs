using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Services.TextGeneration;

namespace MyNovelBuilder.WebApi.Tests.Unit.Services;

public sealed class GoogleReasoningConfigTests
{
    [Theory]
    [InlineData("gemini-2.5-flash", ReasoningEffort.None, 0)]
    [InlineData("models/gemini-2.5-flash-lite", ReasoningEffort.Minimal, 512)]
    [InlineData("gemini-2.5-pro", ReasoningEffort.None, 128)]
    [InlineData("gemini-2.5-flash", ReasoningEffort.Low, 1024)]
    [InlineData("gemini-2.5-pro", ReasoningEffort.Medium, 8192)]
    [InlineData("gemini-2.5-flash", ReasoningEffort.High, 24576)]
    [InlineData("gemini-2.5-pro", ReasoningEffort.Max, 32768)]
    public void Gemini25UsesBudgetInSdkAndRest(string model, ReasoningEffort effort, int budget)
    {
        var rest = GoogleReasoningConfig.Create(model, effort)!;
        var sdk = GoogleReasoningConfig.CreateSdk(model, effort)!;
        Assert.Equal(budget, rest["thinkingBudget"]!.GetValue<int>());
        Assert.Null(rest["thinkingLevel"]);
        Assert.Equal(budget, sdk.ThinkingBudget);
        Assert.Null(sdk.ThinkingLevel);
    }

    [Theory]
    [InlineData("gemini-3-flash-preview", ReasoningEffort.None, "MINIMAL")]
    [InlineData("gemini-3-flash-preview", ReasoningEffort.Medium, "MEDIUM")]
    [InlineData("gemini-3-pro-preview", ReasoningEffort.None, "LOW")]
    [InlineData("gemini-3-pro-preview", ReasoningEffort.Medium, "HIGH")]
    [InlineData("gemini-3.1-pro-preview", ReasoningEffort.Medium, "MEDIUM")]
    [InlineData("gemini-3.8-flash", ReasoningEffort.None, "LOW")]
    [InlineData("gemini-4-carbon", ReasoningEffort.Minimal, "LOW")]
    [InlineData("gemini-3-flash-preview", ReasoningEffort.Max, "HIGH")]
    public void NewGeminiModelsUseSupportedLevelInSdkAndRest(string model, ReasoningEffort effort, string level)
    {
        var rest = GoogleReasoningConfig.Create(model, effort)!;
        var sdk = GoogleReasoningConfig.CreateSdk(model, effort)!;
        Assert.Equal(level, rest["thinkingLevel"]!.GetValue<string>());
        Assert.Null(rest["thinkingBudget"]);
        Assert.Equal(level, sdk.ThinkingLevel.ToString());
        Assert.Null(sdk.ThinkingBudget);
    }

    [Theory]
    [InlineData("gemini-3-flash-preview", ReasoningEffort.ProviderDefault)]
    [InlineData("gemini-2.5-pro", ReasoningEffort.ProviderDefault)]
    [InlineData("gemini-2.0-flash", ReasoningEffort.High)]
    public void DefaultsAndOlderModelsOmitThinkingConfiguration(string model, ReasoningEffort effort)
    {
        Assert.Null(GoogleReasoningConfig.Create(model, effort));
        Assert.Null(GoogleReasoningConfig.CreateSdk(model, effort));
    }
}
