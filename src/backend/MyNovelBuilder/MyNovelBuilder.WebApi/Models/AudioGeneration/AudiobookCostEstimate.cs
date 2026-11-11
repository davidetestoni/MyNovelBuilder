namespace MyNovelBuilder.WebApi.Models.AudioGeneration;

/// <summary>Rough inference charges in USD, excluding stages with unavailable pricing.</summary>
public sealed record AudiobookCostEstimate(DateTimeOffset? PricingAsOf, AudiobookCostRange? Total,
    bool IsPartial, IReadOnlyList<AudiobookCostStage> Stages, IReadOnlyList<string> Notes);

/// <summary>Heuristic range, not a quote or a spending limit.</summary>
public sealed record AudiobookCostRange(decimal Low, decimal High);

/// <summary>A stage is estimated, disabled, or unavailable. Unknown charges are never treated as free.</summary>
public sealed record AudiobookCostStage(string Label, string Status, AudiobookCostRange? Cost, string Basis);
