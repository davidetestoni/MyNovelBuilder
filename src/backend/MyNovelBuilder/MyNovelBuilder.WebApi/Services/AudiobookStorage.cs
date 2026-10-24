namespace MyNovelBuilder.WebApi.Services;

/// <summary>A storage or container limit with a fixed, safe code suitable for job progress.</summary>
/// <param name="code">Actionable error identifier, without filesystem paths.</param>
/// <param name="message">Human-readable explanation of the limit.</param>
public sealed class AudiobookExportException(string code, string message) : IOException(message)
{
    /// <summary>Fixed identifier to persist instead of an exception message.</summary>
    public string Code { get; } = code;
}

/// <summary>Checks available space before writing large audiobook artifacts.</summary>
internal static class AudiobookStorage
{
    internal static void EnsureWaveSize(long length, long additional)
    {
        if (additional > uint.MaxValue - 44L - length)
            throw new AudiobookExportException("wav_size_limit_split_chapter", "Audio exceeds the WAV container limit. Split the chapter into smaller chapters.");
    }

    internal static void EnsureSpace(string directory, long bytes)
    {
        var fullPath = Path.GetFullPath(directory);
        var drive = DriveInfo.GetDrives().Where(d => d.IsReady &&
                (fullPath == d.Name.TrimEnd(Path.DirectorySeparatorChar) ||
                 fullPath.StartsWith((Path.EndsInDirectorySeparator(d.Name) ? d.Name : d.Name + Path.DirectorySeparatorChar), StringComparison.Ordinal)))
            .OrderByDescending(d => d.Name.Length).FirstOrDefault();
        if (drive is not null && drive.AvailableFreeSpace - 1024 * 1024 < bytes)
            throw new AudiobookExportException("insufficient_disk_space", "Free disk space before retrying the audiobook export.");
    }
}
