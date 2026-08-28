using Microsoft.Extensions.Configuration;
using MyNovelBuilder.WebApi.Options;

namespace MyNovelBuilder.WebApi.Storage;

internal enum AppPlatform
{
    Windows,
    MacOS,
    Linux
}

internal sealed class AppDataPathResolver : IAppDataPathResolver
{
    internal const string DataDirectoryEnvironmentVariable = "MYNOVELBUILDER_DATA_DIR";
    private const string ProductDirectoryName = "MyNovelBuilder";

    private readonly AppPlatform platform;
    private readonly Func<string, string?> getEnvironmentVariable;
    private readonly string userProfile;
    private readonly string localApplicationData;

    public AppDataPathResolver()
        : this(
            DetectPlatform(),
            Environment.GetEnvironmentVariable,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
    {
    }

    internal AppDataPathResolver(
        AppPlatform platform,
        Func<string, string?> getEnvironmentVariable,
        string userProfile,
        string localApplicationData)
    {
        this.platform = platform;
        this.getEnvironmentVariable = getEnvironmentVariable;
        this.userProfile = userProfile;
        this.localApplicationData = localApplicationData;
    }

    public string Resolve(IReadOnlyList<string> arguments, IConfiguration configuration)
    {
        var configuredPath = GetCommandLinePath(arguments)
            ?? GetNonEmptyValue(getEnvironmentVariable(DataDirectoryEnvironmentVariable))
            ?? GetNonEmptyValue(configuration[AppStorageOptions.DataFolderKey])
            ?? GetPlatformDefault();

        configuredPath = ExpandHomeDirectory(configuredPath);

        try
        {
            return Path.GetFullPath(configuredPath);
        }
        catch (Exception exception) when (exception is ArgumentException
                                          or NotSupportedException
                                          or PathTooLongException)
        {
            throw new InvalidOperationException(
                $"The configured MyNovelBuilder data directory '{configuredPath}' is invalid.",
                exception);
        }
    }

    private string GetPlatformDefault()
    {
        return platform switch
        {
            AppPlatform.Windows => Path.Combine(
                RequireDirectory(localApplicationData, "the local application data directory"),
                ProductDirectoryName),
            AppPlatform.MacOS => Path.Combine(
                RequireDirectory(userProfile, "the user profile directory"),
                "Library",
                "Application Support",
                ProductDirectoryName),
            AppPlatform.Linux => GetLinuxDefault(),
            _ => throw new PlatformNotSupportedException(
                "MyNovelBuilder does not know where to store application data on this platform.")
        };
    }

    private string GetLinuxDefault()
    {
        var xdgDataHome = GetNonEmptyValue(getEnvironmentVariable("XDG_DATA_HOME"));
        if (xdgDataHome is not null)
        {
            if (!Path.IsPathRooted(xdgDataHome))
            {
                throw new InvalidOperationException(
                    "XDG_DATA_HOME must be an absolute path when it is set.");
            }

            return Path.Combine(xdgDataHome, ProductDirectoryName);
        }

        return Path.Combine(
            RequireDirectory(userProfile, "the user profile directory"),
            ".local",
            "share",
            ProductDirectoryName);
    }

    private string ExpandHomeDirectory(string path)
    {
        if (path == "~")
        {
            return RequireDirectory(userProfile, "the user profile directory");
        }

        if (path.StartsWith("~/", StringComparison.Ordinal)
            || path.StartsWith("~\\", StringComparison.Ordinal))
        {
            return Path.Combine(
                RequireDirectory(userProfile, "the user profile directory"),
                path[2..]);
        }

        return path;
    }

    private static string? GetCommandLinePath(IReadOnlyList<string> arguments)
    {
        string? result = null;

        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (argument.Equals("--data-dir", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= arguments.Count
                    || string.IsNullOrWhiteSpace(arguments[index + 1]))
                {
                    throw new InvalidOperationException(
                        "--data-dir requires a non-empty directory path.");
                }

                result = arguments[index + 1];
                continue;
            }

            const string prefix = "--data-dir=";
            if (argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                result = GetNonEmptyValue(argument[prefix.Length..])
                    ?? throw new InvalidOperationException(
                        "--data-dir requires a non-empty directory path.");
            }
        }

        return result;
    }

    private static string RequireDirectory(string path, string description)
    {
        return GetNonEmptyValue(path)
            ?? throw new InvalidOperationException(
                $"Unable to determine {description} for MyNovelBuilder.");
    }

    private static string? GetNonEmptyValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static AppPlatform DetectPlatform()
    {
        if (OperatingSystem.IsWindows())
        {
            return AppPlatform.Windows;
        }

        if (OperatingSystem.IsMacOS())
        {
            return AppPlatform.MacOS;
        }

        if (OperatingSystem.IsLinux())
        {
            return AppPlatform.Linux;
        }

        throw new PlatformNotSupportedException(
            "MyNovelBuilder supports Windows, macOS, and Linux.");
    }
}
