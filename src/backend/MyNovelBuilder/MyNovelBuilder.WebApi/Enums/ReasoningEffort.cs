namespace MyNovelBuilder.WebApi.Enums;

/// <summary>Requested reasoning intensity; providers map this to their supported controls.</summary>
public enum ReasoningEffort
{
    /// <summary>Leave reasoning controls unspecified.</summary>
    ProviderDefault = 1,
    /// <summary>Disable reasoning, or use the model's lowest supported setting.</summary>
    None = 0,
    /// <summary>Minimal reasoning.</summary>
    Minimal = 2,
    /// <summary>Low reasoning.</summary>
    Low,
    /// <summary>Medium reasoning.</summary>
    Medium,
    /// <summary>High reasoning.</summary>
    High,
    /// <summary>Extra high reasoning.</summary>
    XHigh,
    /// <summary>Maximum reasoning.</summary>
    Max
}
