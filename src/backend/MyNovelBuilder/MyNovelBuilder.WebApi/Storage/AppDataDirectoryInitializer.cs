namespace MyNovelBuilder.WebApi.Storage;

internal sealed class AppDataDirectoryInitializer : IAppDataDirectoryInitializer
{
    private const UnixFileMode PrivateDirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private readonly bool useUnixPermissions;

    public AppDataDirectoryInitializer()
        : this(!OperatingSystem.IsWindows())
    {
    }

    internal AppDataDirectoryInitializer(bool useUnixPermissions)
    {
        this.useUnixPermissions = useUnixPermissions;
    }

    public void Initialize(string dataFolder)
    {
        try
        {
            var directoryAlreadyExists = Directory.Exists(dataFolder);
            if (useUnixPermissions
                && !OperatingSystem.IsWindows()
                && !directoryAlreadyExists)
            {
                Directory.CreateDirectory(dataFolder, PrivateDirectoryMode);
            }
            else
            {
                Directory.CreateDirectory(dataFolder);
            }

            VerifyWritable(dataFolder);
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                $"MyNovelBuilder cannot write to its data directory '{dataFolder}'. "
                + "Choose a writable location with --data-dir or "
                + $"{AppDataPathResolver.DataDirectoryEnvironmentVariable}.",
                exception);
        }
    }

    private static void VerifyWritable(string dataFolder)
    {
        var probePath = Path.Combine(
            dataFolder,
            $".mynovelbuilder-write-test-{Guid.NewGuid():N}.tmp");

        try
        {
            using var probe = new FileStream(
                probePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1,
                FileOptions.DeleteOnClose);
            probe.WriteByte(0);
            probe.Flush(flushToDisk: true);
        }
        finally
        {
            File.Delete(probePath);
        }
    }
}
