using System.ComponentModel.DataAnnotations;

namespace MyNovelBuilder.WebApi.Options;

/// <summary>
/// Storage-related application settings.
/// </summary>
public class AppStorageOptions
{
    /// <summary>
    /// Configuration key for the data folder path.
    /// </summary>
    public const string DataFolderKey = "DataFolder";

    /// <summary>
    /// Root path where application data is stored.
    /// </summary>
    [Required]
    public string DataFolder { get; set; } = string.Empty;

    /// <summary>Days to keep unused generated audio and old ZIPs. Zero disables age expiry; abandoned temporary files are still cleaned.</summary>
    [Range(0, 36500)]
    public int GeneratedAudioRetentionDays { get; set; } = 90;

    /// <summary>
    /// Path to the application SQLite database.
    /// </summary>
    public string DatabasePath => Path.Combine(DataFolder, "app.db");

    /// <summary>
    /// Root path where static files are stored.
    /// </summary>
    public string StaticFilesRoot => Path.Combine(DataFolder, "static");
}
