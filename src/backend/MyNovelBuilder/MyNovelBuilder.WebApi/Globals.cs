namespace MyNovelBuilder.WebApi;

/// <summary>
/// Global variables.
/// </summary>
public static class Globals
{
    /// <summary>
    /// If the application is in testing mode.
    /// </summary>
    public static bool Testing { get; } = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Testing";

    /// <summary>
    /// The root path for static files.
    /// </summary>
    public static string StaticFilesRoot { get; } = Path.Combine(AppContext.BaseDirectory, "wwwroot");
}
