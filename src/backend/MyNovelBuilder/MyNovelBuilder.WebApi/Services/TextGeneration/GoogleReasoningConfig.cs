using System.Text.Json.Nodes;
using Google.GenAI.Types;
using MyNovelBuilder.WebApi.Enums;

namespace MyNovelBuilder.WebApi.Services.TextGeneration;

internal static class GoogleReasoningConfig
{
    // Gemini 2.5 exposes token budgets; Gemini 3+ exposes levels. Older models
    // have no thinking controls. Never send both controls in the same request.
    internal static JsonObject? Create(string model, ReasoningEffort effort)
    {
        if (effort == ReasoningEffort.ProviderDefault) return null;
        var name = model.Replace("models/", "", StringComparison.Ordinal).ToLowerInvariant();
        if (name.StartsWith("gemini-2.5-", StringComparison.Ordinal))
        {
            var pro = name.Contains("-pro", StringComparison.Ordinal);
            var budget = effort switch
            {
                ReasoningEffort.None => pro ? 128 : 0,
                ReasoningEffort.Minimal => pro ? 128 : 512,
                ReasoningEffort.Low => 1024,
                ReasoningEffort.Medium => 8192,
                _ => pro ? 32768 : 24576
            };
            return new JsonObject { ["thinkingBudget"] = budget };
        }
        if (!name.StartsWith("gemini-3", StringComparison.Ordinal)
            && !name.StartsWith("gemini-4", StringComparison.Ordinal)) return null;

        var supportsMinimal = name.Contains("flash", StringComparison.Ordinal)
            && !name.StartsWith("gemini-3.8-", StringComparison.Ordinal)
            && !name.StartsWith("gemini-3.7-", StringComparison.Ordinal);
        var level = effort switch
        {
            ReasoningEffort.None or ReasoningEffort.Minimal => supportsMinimal ? "MINIMAL" : "LOW",
            ReasoningEffort.Low => "LOW",
            ReasoningEffort.Medium => name.StartsWith("gemini-3-pro", StringComparison.Ordinal) ? "HIGH" : "MEDIUM",
            _ => "HIGH"
        };
        return new JsonObject { ["thinkingLevel"] = level };
    }

    internal static ThinkingConfig? CreateSdk(string model, ReasoningEffort effort)
    {
        var config = Create(model, effort);
        if (config is null) return null;
        return config["thinkingBudget"] is { } budget
            ? new ThinkingConfig { ThinkingBudget = budget.GetValue<int>() }
            : new ThinkingConfig { ThinkingLevel = Enum.Parse<ThinkingLevel>(config["thinkingLevel"]!.GetValue<string>()) };
    }
}
