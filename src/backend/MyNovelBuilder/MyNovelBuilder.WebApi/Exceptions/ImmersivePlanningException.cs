namespace MyNovelBuilder.WebApi.Exceptions;

/// <summary>The planning model repeatedly returned an invalid partition of the original narration.</summary>
/// <param name="attempts">Number of unsuccessful planning attempts.</param>
/// <param name="reason">Validation details, including bounded source and output excerpts when available.</param>
public sealed class ImmersivePlanningException(int attempts, string reason)
    : Exception($"Immersive planning failed validation after {attempts} attempts. {reason}");
