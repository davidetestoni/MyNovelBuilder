#if ELECTRON_DESKTOP
using ElectronNET;
using ElectronNET.API;
using ElectronNET.API.Entities;
#endif

namespace MyNovelBuilder.WebApi.Desktop;

internal static class ElectronDesktop
{
    private static readonly string[] DotnetFirstFlags =
    [
        "-unpackeddotnet",
        "--unpackeddotnet",
        "-dotnetpacked",
        "--dotnetpacked"
    ];

    internal static bool IsRequested(IReadOnlyCollection<string> arguments)
    {
        return arguments.Any(argument =>
            DotnetFirstFlags.Contains(argument, StringComparer.OrdinalIgnoreCase) ||
            argument.StartsWith(
                "/electronPort=",
                StringComparison.OrdinalIgnoreCase));
    }

    internal static void Configure(
        WebApplicationBuilder builder,
        string[] arguments)
    {
        if (!IsRequested(arguments))
        {
            return;
        }

#if ELECTRON_DESKTOP
        builder.UseElectron(arguments, CreateMainWindowAsync);
#else
        throw new InvalidOperationException(
            "Electron startup was requested from a non-desktop build.");
#endif
    }

#if ELECTRON_DESKTOP
    private static async Task CreateMainWindowAsync()
    {
        var options = new BrowserWindowOptions
        {
            Title = "MyNovelBuilder",
            Width = 1440,
            Height = 960,
            MinWidth = 1024,
            MinHeight = 700,
            Show = false,
            WebPreferences = new WebPreferences
            {
                ContextIsolation = true,
                NodeIntegration = false,
                Sandbox = true
            }
        };

        if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
        {
            options.AutoHideMenuBar = true;
        }

        var browserWindow = await Electron.WindowManager
            .CreateWindowAsync(options);
        browserWindow.OnReadyToShow += () => browserWindow.Show();
    }
#endif
}
