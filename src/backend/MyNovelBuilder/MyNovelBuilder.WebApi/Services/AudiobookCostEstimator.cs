using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Models.AudioGeneration;

namespace MyNovelBuilder.WebApi.Services;

/// <summary>Estimates uncached sections using public OpenRouter rates without running paid inference.</summary>
public sealed class AudiobookCostEstimator(HttpClient http, IMemoryCache cache, ILogger<AudiobookCostEstimator> logger)
{
    private const string CatalogueKey = "audiobook-openrouter-pricing-v1";

    /// <summary>Fully reusable sections are excluded; partial chunk/preparation reuse may reduce the actual bill further.</summary>
    public async Task<AudiobookCostEstimate> EstimateAsync(ResolvedAudiobookSettings settings,
        IReadOnlyList<AudiobookSectionSnapshot> pending, CancellationToken ct)
    {
        Catalogue? catalogue = null;
        if (pending.Count > 0 && (settings.Provider == TtsProvider.OpenRouter
            || ((settings.EnableImmersive || settings.EnableTextEmphasis) && settings.TextGenerationProvider == TextGenerationProvider.OpenRouter)))
        {
            try
            {
                catalogue = await cache.GetOrCreateAsync(CatalogueKey, async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                    // The public catalogue requires no credentials and contains no manuscript data.
                    var json = await http.GetStringAsync("https://openrouter.ai/api/v1/models?output_modalities=text,speech", ct);
                    var root = JsonNode.Parse(json) as JsonObject;
                    var models = root?["data"] as JsonArray
                        ?? throw new FormatException("Missing model catalogue.");
                    return new Catalogue(DateTimeOffset.UtcNow, models);
                });
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or FormatException)
            {
                // Pricing is optional: a catalogue outage must not prevent audiobook generation.
                logger.LogWarning(ex, "Audiobook pricing unavailable.");
            }
        }
        return Calculate(settings, pending, catalogue?.Models, catalogue?.At);
    }

    /// <summary>Pure calculation over a rate catalogue and frozen section inputs.</summary>
    public static AudiobookCostEstimate Calculate(ResolvedAudiobookSettings settings,
        IReadOnlyList<AudiobookSectionSnapshot> pending, JsonArray? models, DateTimeOffset? pricingAsOf = null)
    {
        var stages = new List<AudiobookCostStage>();
        var profile = settings.EnableTextEmphasis ? TtsAnnotationProfile.Resolve(settings.Provider, settings.ModelId) : null;
        var characters = pending.Sum(s => (decimal)s.SpeechText.Length);
        var words = pending.Sum(s => (decimal)Regex.Count(s.SpeechText, @"\S+"));
        // 120–180 words/minute. Locally inserted gaps are not billed generated audio.
        var seconds = new AudiobookCostRange(words / 180 * 60, words / 120 * 60);
        var speechCharacters = new AudiobookCostRange(characters, characters * (profile is null ? 1 : 1.15m));
        var speech = settings.Provider == TtsProvider.OpenRouter ? Find(models, settings.ModelId) : null;
        stages.Add(pending.Count == 0 ? Zero("Speech synthesis") : Speech(speech, speechCharacters, seconds));

        var text = settings.TextGenerationProvider == TextGenerationProvider.OpenRouter ? Find(models, settings.TextGenerationModelId) : null;
        var emphasis = Disabled("Emphasis");
        if (profile is not null)
            emphasis = pending.Count == 0 ? Zero("Emphasis") : TextStage("Emphasis", text,
                pending.Sum(s => (decimal)profile.Messages(s.SpeechText).Sum(m => m.Message.Length + 16)),
                characters, 1.15m);
        stages.Add(emphasis);
        // Include rendered story context, message overhead, schema and code-owned planning instructions.
        var planning = Disabled("Dialogue / narration planning");
        var annotationOverhead = profile is null ? 0 : characters * .15m;
        if (settings.EnableImmersive)
            planning = pending.Count == 0 ? Zero("Dialogue / narration planning") : TextStage("Dialogue / narration planning", text,
                pending.Sum(s => (decimal)s.ImmersivePrompt.Sum(m => m.Message.Length + 16) + 4000)
                    + annotationOverhead,
                speechCharacters.High, 2m);
        stages.Add(planning);
        var known = stages.Where(s => s.Status == "estimated").ToArray();
        return new(pricingAsOf, known.Length == 0 ? null : new(known.Sum(s => s.Cost!.Low), known.Sum(s => s.Cost!.High)),
            stages.Any(s => s.Status == "unavailable"), stages,
            ["Rough USD inference estimate for sections needing generation. Fully reusable sections are excluded; partial audio or preparation reuse may lower the bill.",
             "Text uses roughly 3–5 characters/token, estimated output and prompt overhead; audio uses 120–180 words/minute. Emphasis allows up to 15% added delivery tags; speaker plans allow JSON overhead.",
             "Retries, reasoning tokens, provider routing differences, credit purchase fees and taxes are excluded. This range is not a spending limit."]);
    }

    private static AudiobookCostStage Speech(JsonNode? model, AudiobookCostRange chars, AudiobookCostRange seconds)
    {
        var input = Rate(model, "prompt");
        var output = Rate(model, "completion");
        if (input is null || output is null) return Unknown("Speech synthesis");
        if ((Rate(model, "request") ?? 0) != 0) return Unknown("Speech synthesis");
        var id = model?["id"]?.ToString() ?? "";
        if (output == 0)
            return Estimated("Speech synthesis", new(chars.Low * input.Value, chars.High * input.Value), "OpenRouter rate per input character; includes estimated delivery tags.");
        // OpenRouter overloads completion: Seed bills seconds; Gemini bills audio tokens.
        // Never silently interpret an unknown audio-token rate as a per-second rate.
        if (id.StartsWith("bytedance-seed/seed-audio-1-0", StringComparison.Ordinal))
            return Estimated("Speech synthesis", new(chars.Low * input.Value + seconds.Low * output.Value,
                chars.High * input.Value + seconds.High * output.Value), "OpenRouter rate per generated second; excludes locally added gaps.");
        if (id.StartsWith("google/gemini-", StringComparison.Ordinal) && id.Contains("tts", StringComparison.Ordinal))
            // https://cloud.google.com/text-to-speech/pricing: 25 output audio tokens/second.
            return Estimated("Speech synthesis", new(chars.Low / 5 * input.Value + seconds.Low * 25 * output.Value,
                chars.High / 3 * input.Value + seconds.High * 25 * output.Value), "OpenRouter text/audio token rates; Gemini audio estimated at 25 tokens/second.");
        return Unknown("Speech synthesis", "This model's audio billing unit is not supported by the estimator yet.");
    }

    private static AudiobookCostStage TextStage(string label, JsonNode? model, decimal inputCharacters, decimal outputCharacters, decimal outputExpansion)
    {
        var input = Rate(model, "prompt");
        var output = Rate(model, "completion");
        var request = Rate(model, "request") ?? 0;
        if (input is null || output is null || request != 0) return Unknown(label);
        return Estimated(label, new(inputCharacters / 5 * input.Value + outputCharacters / 5 * output.Value,
            inputCharacters / 3 * input.Value + outputCharacters * outputExpansion / 3 * output.Value),
            "OpenRouter input/output token rates; full prompts and estimated output, one successful pass.");
    }

    private static JsonNode? Find(JsonArray? models, string id) => models?.OfType<JsonObject>().FirstOrDefault(m => m["id"]?.ToString() == id);
    private static decimal? Rate(JsonNode? model, string name) =>
        decimal.TryParse((model?["pricing"] as JsonObject)?[name]?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value >= 0 ? value : null;
    private static AudiobookCostStage Estimated(string label, AudiobookCostRange cost, string basis) => new(label, "estimated", cost, basis);
    private static AudiobookCostStage Zero(string label) => Estimated(label, new(0, 0), "All sections have reusable audio.");
    private static AudiobookCostStage Disabled(string label) => new(label, "disabled", null, "Off for these settings.");
    private static AudiobookCostStage Unknown(string label, string basis = "Pricing unavailable. Automatic estimates currently use supported OpenRouter model rates.") => new(label, "unavailable", null, basis);
    private sealed record Catalogue(DateTimeOffset At, JsonArray Models);
}
