# Install the desktop application

MyNovelBuilder desktop packages contain the application and its required .NET
and Electron runtimes. You do not need to install Node.js, .NET, or Docker.

The packages are currently unsigned. Windows SmartScreen, macOS Gatekeeper, or
your Linux desktop may therefore ask you to confirm that you trust the
application. Download releases only from the project's
[GitHub Releases page](https://github.com/davidetestoni/MyNovelBuilder/releases/latest).

## Choose the correct package

| System | Processor | Package |
| --- | --- | --- |
| Windows | Intel or AMD 64-bit | `MyNovelBuilder-Windows-x64-Setup.exe` |
| Windows | ARM64 | `MyNovelBuilder-Windows-arm64-Setup.exe` |
| macOS | Intel | `MyNovelBuilder-macOS-x64.dmg` |
| macOS | Apple silicon | `MyNovelBuilder-macOS-arm64.dmg` |
| Linux | Intel or AMD 64-bit | `MyNovelBuilder-Linux-x64.deb` or `.AppImage` |
| Linux | ARM64 | `MyNovelBuilder-Linux-arm64.deb` or `.AppImage` |

Most Windows PCs use x64. Check **Settings > System > About > System type** if
you are unsure. On macOS, open **Apple menu > About This Mac** and look for
either an Intel processor or an Apple chip. On Linux, `uname -m` normally
reports `x86_64` for x64 and `aarch64` for ARM64.

Each download has a matching `.sha256` file on the release page. Verifying it
is optional, but it confirms that the package was downloaded intact.

## Windows

1. Download the Setup executable for your processor.
2. Run the downloaded file.
3. If Microsoft Defender SmartScreen appears, select **More info**, confirm
   that the application name is MyNovelBuilder, and select **Run anyway**.
4. Complete the installer and open MyNovelBuilder.

You can remove the application later from **Settings > Apps > Installed apps**.
Uninstalling does not remove your novels or settings.

## macOS

1. Download the DMG for Intel or Apple silicon.
2. Open the DMG and drag MyNovelBuilder into Applications.
3. In Finder, Control-click MyNovelBuilder and choose **Open**.
4. Confirm **Open** when macOS warns that the developer cannot be verified.

If macOS does not offer that confirmation, open **System Settings > Privacy &
Security** and use **Open Anyway** for MyNovelBuilder. Removing the application
from Applications does not remove your novels or settings.

## Linux

The DEB package is recommended on Debian and Ubuntu systems. From the directory
containing the download, install it with:

```shell
sudo apt install ./MyNovelBuilder-Linux-x64.deb
```

Use the ARM64 filename instead on ARM64. The installed application can be
started from the desktop application menu.

The AppImage is useful on other distributions:

```shell
chmod +x MyNovelBuilder-Linux-x64.AppImage
./MyNovelBuilder-Linux-x64.AppImage
```

Some Ubuntu configurations prevent the AppImage Chromium sandbox from
starting. Use the DEB package if the AppImage reports a sandbox or user
namespace error. Do not work around this by disabling the sandbox.

## First launch and application data

The initial workspace contains editable starter prompts and an optional sample
novel. Writing and organization features work without an API key. AI features
use provider or local-server settings that you configure yourself.

Application data is stored separately from the installed program:

| System | Default data directory |
| --- | --- |
| Windows | `%LOCALAPPDATA%\MyNovelBuilder` |
| macOS | `~/Library/Application Support/MyNovelBuilder` |
| Linux | `$XDG_DATA_HOME/MyNovelBuilder`, or `~/.local/share/MyNovelBuilder` when that variable is unset |

Installing a newer package or uninstalling the application leaves this
directory in place. To make a complete manual backup, close MyNovelBuilder and
copy the entire directory. Integration credentials are stored as plaintext in
`integrations.json`, so do not share an application-data backup publicly.

## Update

Download the newer package for the same platform and install it over the
existing version. MyNovelBuilder applies database migrations at startup and
creates a timestamped database backup before changing an existing database.

Check the release notes for version-specific warnings before updating. Keep a
separate copy of important writing even though the application preserves its
data across package upgrades.
