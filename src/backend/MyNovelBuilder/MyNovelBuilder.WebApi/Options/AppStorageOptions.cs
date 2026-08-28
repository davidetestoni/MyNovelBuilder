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

    /// <summary>
    /// Root path where static files are stored.
    /// </summary>
    public string StaticFilesRoot => Path.Combine(DataFolder, "static");
}
