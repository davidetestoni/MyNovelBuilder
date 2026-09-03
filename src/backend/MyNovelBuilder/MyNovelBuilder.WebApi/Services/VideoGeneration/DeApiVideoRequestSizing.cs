namespace MyNovelBuilder.WebApi.Services.VideoGeneration;

/// <summary>
/// Provider-specific request sizing rules for deAPI video generation.
/// </summary>
public static class DeApiVideoRequestSizing
{
    /// <summary>
    /// deAPI rejects video generation dimensions above this limit.
    /// </summary>
    public const int MaxDimension = 1024;

    /// <summary>
    /// Fit requested dimensions within deAPI's maximum bounds while preserving aspect ratio.
    /// </summary>
    public static (int Width, int Height) FitWithinLimits(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);

        if (width <= MaxDimension && height <= MaxDimension)
        {
            return (width, height);
        }

        var scale = Math.Min(
            (double)MaxDimension / width,
            (double)MaxDimension / height);

        return
        (
            Width: ClampDimension(width * scale),
            Height: ClampDimension(height * scale)
        );
    }

    private static int ClampDimension(double value)
    {
        return Math.Clamp(
            (int)Math.Round(value, MidpointRounding.AwayFromZero),
            1,
            MaxDimension);
    }
}
