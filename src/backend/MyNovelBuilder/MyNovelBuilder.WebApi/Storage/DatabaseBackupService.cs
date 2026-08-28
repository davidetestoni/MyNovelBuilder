using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using MyNovelBuilder.WebApi.Options;

namespace MyNovelBuilder.WebApi.Storage;

internal sealed class DatabaseBackupService(
    IOptions<AppStorageOptions> storageOptions,
    IAppDataDirectoryInitializer directoryInitializer,
    TimeProvider timeProvider,
    ILogger<DatabaseBackupService> logger) : IDatabaseBackupService
{
    internal const int MaximumRetainedBackups = 5;

    private const UnixFileMode PrivateFileMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite;

    public string? CreateBackup()
    {
        var databasePath = storageOptions.Value.DatabasePath;
        if (!File.Exists(databasePath))
        {
            return null;
        }

        var backupDirectory = Path.Combine(storageOptions.Value.DataFolder, "backups", "database");
        directoryInitializer.Initialize(backupDirectory);

        var timestamp = timeProvider.GetUtcNow().ToString(
            "yyyyMMdd'T'HHmmssfffffff'Z'",
            CultureInfo.InvariantCulture);
        var backupPath = Path.Combine(
            backupDirectory,
            $"app-{timestamp}-{Guid.NewGuid():N}.db");

        try
        {
            using var source = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly
            }.ConnectionString);
            using var destination = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = backupPath,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ConnectionString);

            source.Open();
            destination.Open();
            source.BackupDatabase(destination);
            ValidateBackup(destination, backupPath);

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(backupPath, PrivateFileMode);
            }
        }
        catch (Exception exception) when (exception is SqliteException
                                          or IOException
                                          or UnauthorizedAccessException)
        {
            TryDeleteFailedBackup(backupPath);
            throw new InvalidOperationException(
                $"MyNovelBuilder could not back up its database to '{backupPath}'. "
                + "The database migration was not started.",
                exception);
        }

        PruneOldBackups(backupDirectory);
        logger.LogInformation("Created database backup at {BackupPath}", backupPath);
        return backupPath;
    }

    private static void ValidateBackup(SqliteConnection destination, string backupPath)
    {
        using var command = destination.CreateCommand();
        command.CommandText = "PRAGMA quick_check;";
        var result = Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture);

        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"SQLite validation failed for database backup '{backupPath}'.");
        }
    }

    private void PruneOldBackups(string backupDirectory)
    {
        var expiredBackups = Directory
            .EnumerateFiles(backupDirectory, "app-*.db", SearchOption.TopDirectoryOnly)
            .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
            .Skip(MaximumRetainedBackups);

        foreach (var expiredBackup in expiredBackups)
        {
            try
            {
                File.Delete(expiredBackup);
            }
            catch (Exception exception) when (exception is IOException
                                              or UnauthorizedAccessException)
            {
                logger.LogWarning(
                    exception,
                    "Could not remove expired database backup {BackupPath}",
                    expiredBackup);
            }
        }
    }

    private void TryDeleteFailedBackup(string backupPath)
    {
        try
        {
            File.Delete(backupPath);
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException)
        {
            logger.LogWarning(
                exception,
                "Could not remove incomplete database backup {BackupPath}",
                backupPath);
        }
    }
}
