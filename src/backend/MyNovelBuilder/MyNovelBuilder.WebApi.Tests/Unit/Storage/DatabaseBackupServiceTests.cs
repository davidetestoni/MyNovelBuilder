using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using MyNovelBuilder.WebApi.Options;
using MyNovelBuilder.WebApi.Storage;

namespace MyNovelBuilder.WebApi.Tests.Unit.Storage;

public sealed class DatabaseBackupServiceTests : IDisposable
{
    private readonly string dataFolder = Path.Combine(
        Path.GetTempPath(),
        $"mynovelbuilder-backup-tests-{Guid.NewGuid():N}");

    [Fact]
    public void CreateBackup_ReturnsNullWhenDatabaseDoesNotExist()
    {
        var service = CreateService();

        var backupPath = service.CreateBackup();

        Assert.Null(backupPath);
        Assert.False(Directory.Exists(Path.Combine(dataFolder, "backups")));
    }

    [Fact]
    public void CreateBackup_CapturesCommittedWalDataAndProducesValidDatabase()
    {
        Directory.CreateDirectory(dataFolder);
        using var source = new SqliteConnection($"Data Source={GetDatabasePath()}");
        source.Open();
        Execute(source, "PRAGMA journal_mode = WAL;");
        Execute(source, "CREATE TABLE TestRecords (Value TEXT NOT NULL);");
        Execute(source, "INSERT INTO TestRecords (Value) VALUES ('preserved');");
        var service = CreateService();

        var backupPath = service.CreateBackup();

        Assert.NotNull(backupPath);
        Assert.True(File.Exists(backupPath));
        using var backup = new SqliteConnection($"Data Source={backupPath};Mode=ReadOnly");
        backup.Open();
        using var command = backup.CreateCommand();
        command.CommandText = "SELECT Value FROM TestRecords;";
        Assert.Equal("preserved", command.ExecuteScalar());
    }

    [Fact]
    public void CreateBackup_RetainsOnlyNewestFiveBackups()
    {
        CreateSourceDatabase();
        var backupDirectory = Path.Combine(dataFolder, "backups", "database");
        Directory.CreateDirectory(backupDirectory);
        for (var day = 1; day <= DatabaseBackupService.MaximumRetainedBackups; day++)
        {
            File.WriteAllText(
                Path.Combine(backupDirectory, $"app-2025010{day}T0000000000000Z-old.db"),
                "old backup");
        }
        var service = CreateService();

        var backupPath = service.CreateBackup();

        var backups = Directory.GetFiles(backupDirectory, "app-*.db");
        Assert.Equal(DatabaseBackupService.MaximumRetainedBackups, backups.Length);
        Assert.Contains(backupPath, backups);
        Assert.DoesNotContain(
            Path.Combine(backupDirectory, "app-20250101T0000000000000Z-old.db"),
            backups);
    }

    [Fact]
    public void CreateBackup_UsesOwnerOnlyPermissionsOnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        CreateSourceDatabase();
        var service = CreateService();

        var backupPath = service.CreateBackup();

        Assert.NotNull(backupPath);
        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite,
            File.GetUnixFileMode(backupPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(dataFolder))
        {
            Directory.Delete(dataFolder, recursive: true);
        }
    }

    private DatabaseBackupService CreateService()
    {
        return new DatabaseBackupService(
            Microsoft.Extensions.Options.Options.Create(
                new AppStorageOptions { DataFolder = dataFolder }),
            new AppDataDirectoryInitializer(useUnixPermissions: false),
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero)),
            NullLogger<DatabaseBackupService>.Instance);
    }

    private void CreateSourceDatabase()
    {
        Directory.CreateDirectory(dataFolder);
        using var connection = new SqliteConnection($"Data Source={GetDatabasePath()}");
        connection.Open();
        Execute(connection, "CREATE TABLE TestRecords (Value TEXT NOT NULL);");
    }

    private string GetDatabasePath() => Path.Combine(dataFolder, "app.db");

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
