using Microsoft.Extensions.Configuration;
using MyNovelBuilder.WebApi.Options;
using MyNovelBuilder.WebApi.Storage;

namespace MyNovelBuilder.WebApi.Tests.Unit.Storage;

public class AppDataPathResolverTests
{
    private const string UserProfile = "/users/writer";
    private const string LocalApplicationData = "/windows/local-app-data";

    [Fact]
    public void Resolve_CommandLinePathTakesPrecedence()
    {
        var resolver = CreateResolver(
            AppPlatform.Linux,
            new Dictionary<string, string?>
            {
                [AppDataPathResolver.DataDirectoryEnvironmentVariable] = "/environment/data"
            });
        var configuration = CreateConfiguration("/configuration/data");

        var result = resolver.Resolve(["--data-dir", "/command-line/data"], configuration);

        Assert.Equal("/command-line/data", result);
    }

    [Fact]
    public void Resolve_AcceptsEqualsCommandLineSyntax()
    {
        var resolver = CreateResolver(AppPlatform.Linux);

        var result = resolver.Resolve(
            ["--data-dir=/command-line/data with spaces"],
            CreateConfiguration());

        Assert.Equal("/command-line/data with spaces", result);
    }

    [Fact]
    public void Resolve_EnvironmentPathTakesPrecedenceOverConfiguration()
    {
        var resolver = CreateResolver(
            AppPlatform.Linux,
            new Dictionary<string, string?>
            {
                [AppDataPathResolver.DataDirectoryEnvironmentVariable] = "/environment/data"
            });

        var result = resolver.Resolve([], CreateConfiguration("/configuration/data"));

        Assert.Equal("/environment/data", result);
    }

    [Fact]
    public void Resolve_PreservesExistingConfigurationOverride()
    {
        var resolver = CreateResolver(AppPlatform.Linux);

        var result = resolver.Resolve([], CreateConfiguration("/configuration/data"));

        Assert.Equal("/configuration/data", result);
    }

    [Fact]
    public void Resolve_UsesWindowsLocalApplicationDataByDefault()
    {
        var resolver = CreateResolver(AppPlatform.Windows);

        var result = resolver.Resolve([], CreateConfiguration());

        Assert.Equal(
            Path.Combine(LocalApplicationData, "MyNovelBuilder"),
            result);
    }

    [Fact]
    public void Resolve_UsesMacApplicationSupportByDefault()
    {
        var resolver = CreateResolver(AppPlatform.MacOS);

        var result = resolver.Resolve([], CreateConfiguration());

        Assert.Equal(
            Path.Combine(UserProfile, "Library", "Application Support", "MyNovelBuilder"),
            result);
    }

    [Fact]
    public void Resolve_UsesLinuxXdgDataHomeByDefaultWhenSet()
    {
        var resolver = CreateResolver(
            AppPlatform.Linux,
            new Dictionary<string, string?> { ["XDG_DATA_HOME"] = "/xdg/data" });

        var result = resolver.Resolve([], CreateConfiguration());

        Assert.Equal(Path.Combine("/xdg/data", "MyNovelBuilder"), result);
    }

    [Fact]
    public void Resolve_UsesLinuxUserDataDirectoryWhenXdgDataHomeIsNotSet()
    {
        var resolver = CreateResolver(AppPlatform.Linux);

        var result = resolver.Resolve([], CreateConfiguration());

        Assert.Equal(
            Path.Combine(UserProfile, ".local", "share", "MyNovelBuilder"),
            result);
    }

    [Fact]
    public void Resolve_ExpandsHomeDirectoryOverride()
    {
        var resolver = CreateResolver(AppPlatform.Linux);

        var result = resolver.Resolve(["--data-dir", "~/novel data"], CreateConfiguration());

        Assert.Equal(Path.Combine(UserProfile, "novel data"), result);
    }

    [Fact]
    public void Resolve_RejectsRelativeXdgDataHome()
    {
        var resolver = CreateResolver(
            AppPlatform.Linux,
            new Dictionary<string, string?> { ["XDG_DATA_HOME"] = "relative/data" });

        var exception = Assert.Throws<InvalidOperationException>(
            () => resolver.Resolve([], CreateConfiguration()));

        Assert.Contains("absolute path", exception.Message);
    }

    [Theory]
    [InlineData("--data-dir")]
    [InlineData("--data-dir=")]
    public void Resolve_RejectsMissingCommandLinePath(string argument)
    {
        var resolver = CreateResolver(AppPlatform.Linux);

        var exception = Assert.Throws<InvalidOperationException>(
            () => resolver.Resolve([argument], CreateConfiguration()));

        Assert.Contains("non-empty directory path", exception.Message);
    }

    private static AppDataPathResolver CreateResolver(
        AppPlatform platform,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        environment ??= new Dictionary<string, string?>();
        return new AppDataPathResolver(
            platform,
            name => environment.GetValueOrDefault(name),
            UserProfile,
            LocalApplicationData);
    }

    private static IConfiguration CreateConfiguration(string? dataFolder = null)
    {
        var values = new Dictionary<string, string?>();
        if (dataFolder is not null)
        {
            values[AppStorageOptions.DataFolderKey] = dataFolder;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
