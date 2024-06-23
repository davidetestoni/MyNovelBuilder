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
    /// The path to the data folder.
    /// </summary>
    public static string DataFolder { get; set; } = Path.Combine(AppContext.BaseDirectory, "data");
    
    /// <summary>
    /// The root path for static files.
    /// </summary>
    public static string StaticFilesRoot => Path.Combine(DataFolder, "static");
}
