using MyNovelBuilder.WebApi.Storage;

namespace MyNovelBuilder.WebApi.Tests.Unit.Storage;

public sealed class AppDataDirectoryInitializerTests : IDisposable
{
    private readonly string testRoot = Path.Combine(
        Path.GetTempPath(),
        $"mynovelbuilder-storage-tests-{Guid.NewGuid():N}");

    [Fact]
    public void Initialize_CreatesWritableDirectoryWithoutLeavingProbeFile()
    {
        var dataFolder = Path.Combine(testRoot, "new-data-folder");
        var initializer = new AppDataDirectoryInitializer(useUnixPermissions: false);

        initializer.Initialize(dataFolder);

        Assert.True(Directory.Exists(dataFolder));
        Assert.Empty(Directory.GetFiles(dataFolder));
    }

    [Fact]
    public void Initialize_DoesNotReplaceExistingDirectoryContents()
    {
        var dataFolder = Path.Combine(testRoot, "existing-data-folder");
        Directory.CreateDirectory(dataFolder);
        var existingFile = Path.Combine(dataFolder, "existing.txt");
        File.WriteAllText(existingFile, "keep me");
        var initializer = new AppDataDirectoryInitializer(useUnixPermissions: false);

        initializer.Initialize(dataFolder);

        Assert.Equal("keep me", File.ReadAllText(existingFile));
        Assert.Single(Directory.GetFiles(dataFolder));
    }

    [Fact]
    public void Initialize_ReturnsActionableErrorWhenPathIsAFile()
    {
        Directory.CreateDirectory(testRoot);
        var filePath = Path.Combine(testRoot, "not-a-directory");
        File.WriteAllText(filePath, "content");
        var initializer = new AppDataDirectoryInitializer(useUnixPermissions: false);

        var exception = Assert.Throws<InvalidOperationException>(
            () => initializer.Initialize(filePath));

        Assert.Contains("cannot write", exception.Message);
        Assert.Contains("--data-dir", exception.Message);
        Assert.Contains(
            AppDataPathResolver.DataDirectoryEnvironmentVariable,
            exception.Message);
        Assert.IsType<IOException>(exception.InnerException);
    }

    [Fact]
    public void Initialize_CreatesPrivateDirectoryOnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var dataFolder = Path.Combine(testRoot, "private-data-folder");
        var initializer = new AppDataDirectoryInitializer(useUnixPermissions: true);

        initializer.Initialize(dataFolder);

        var mode = File.GetUnixFileMode(dataFolder);
        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            mode);
    }

    public void Dispose()
    {
        if (Directory.Exists(testRoot))
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }
}
