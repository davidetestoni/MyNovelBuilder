# Development setup

This guide runs the Angular frontend and ASP.NET Core backend from a clean
checkout. Commands are issued from the repository root unless a step says
otherwise.

## Prerequisites

- Git
- .NET SDK `10.0.100` or a newer compatible 10.0 patch selected by
  `global.json`
- Node.js 22; the repository `.nvmrc` contains the expected major version
- npm, supplied with Node.js
- Google Chrome or Chromium when running frontend tests
- Docker Engine or Docker Desktop only for Docker builds and tests

Node.js 24 is also accepted by the frontend package metadata, but Node 22 is
the project's checked-in development default.

Confirm the core tools:

```shell
dotnet --version
node --version
npm --version
```

## Clean-clone quick start

```shell
git clone https://github.com/davidetestoni/MyNovelBuilder.git
cd MyNovelBuilder
node scripts/tasks.mjs restore
node scripts/tasks.mjs dev
```

Open <http://localhost:4200>. The task starts the backend at
<http://localhost:5113> and Angular's development server at port `4200`.
Angular proxies `/api` and `/static` requests to ASP.NET Core. Press `Ctrl+C`
once to stop both processes.

The application starts without provider credentials. A new database receives
the default editable prompts and sample novel once.

## Development data

Development mode intentionally uses the backend project's local `AppData`
directory. It is ignored by Git. For an isolated session, set
`MYNOVELBUILDER_DATA_DIR` before starting the task.

Linux or macOS:

```shell
export MYNOVELBUILDER_DATA_DIR="$(mktemp -d)"
node scripts/tasks.mjs dev
```

Windows PowerShell:

```powershell
$env:MYNOVELBUILDER_DATA_DIR = Join-Path $env:TEMP ("mynovelbuilder-dev-" + [guid]::NewGuid())
node scripts/tasks.mjs dev
```

The data-directory precedence is:

1. `--data-dir <path>` or `--data-dir=<path>` passed to the backend
2. `MYNOVELBUILDER_DATA_DIR`
3. the configured `DataFolder` value
4. the platform default

The resolved directory is printed at startup. Outside Development mode, the
defaults are `%LOCALAPPDATA%\MyNovelBuilder` on Windows,
`~/Library/Application Support/MyNovelBuilder` on macOS, and
`$XDG_DATA_HOME/MyNovelBuilder` on Linux when `XDG_DATA_HOME` is set, otherwise
`~/.local/share/MyNovelBuilder`.

Everything in the selected directory is user data. In particular,
`integrations.json` contains plaintext provider credentials. Do not commit,
share, or attach it to bug reports.

## Run the services manually

Restore and start the backend in one terminal:

```shell
dotnet restore src/backend/MyNovelBuilder/MyNovelBuilder.WebApi/MyNovelBuilder.WebApi.csproj --locked-mode
dotnet watch run --project src/backend/MyNovelBuilder/MyNovelBuilder.WebApi/MyNovelBuilder.WebApi.csproj --launch-profile http --no-restore
```

Install and start Angular in another terminal:

```shell
cd src/frontend/my-novel-builder
npm ci
npm start
```

Use the Angular URL at <http://localhost:4200> during development. Opening the
backend URL directly does not provide Angular live reload.

## Build and test

The repository task runner uses locked dependency restores:

```shell
node scripts/tasks.mjs test
node scripts/tasks.mjs build
```

`test` runs the desktop-shell checks, backend unit/integration tests, and
frontend headless-browser tests. On Linux, set `CHROME_BIN` if Chrome or
Chromium is installed somewhere other than the common system locations.

Run one side independently when iterating:

```shell
dotnet test src/backend/MyNovelBuilder/MyNovelBuilder.WebApi.Tests/MyNovelBuilder.WebApi.Tests.csproj --configuration Release
```

```shell
cd src/frontend/my-novel-builder
npm test -- --watch=false --browsers=ChromeHeadless
```

## Production-style local publish

Build the Angular SPA and publish it inside the ASP.NET Core application:

```shell
node scripts/tasks.mjs publish-web
dotnet artifacts/publish/web/MyNovelBuilder.WebApi.dll
```

Open <http://localhost:5113>. The output under `artifacts/publish/web` is
disposable and ignored by Git. The publish contains neither development
configuration nor local application data.

## Electron development

To build and open the desktop application for the current operating system and
CPU architecture:

```shell
node scripts/tasks.mjs desktop-dev
```

This downloads Electron into ignored local output on first use. Linux's
unpackaged development command uses `--no-sandbox` because an npm-installed
Chromium helper cannot be installed setuid; packaged releases do not disable
the sandbox.

Build an unsigned package only on its target operating system:

```shell
node scripts/tasks.mjs package-desktop linux-x64
```

Replace `linux-x64` with `linux-arm64`, `win-x64`, `win-arm64`, `osx-x64`, or
`osx-arm64` as appropriate. Outputs go under
`artifacts/desktop/packages/<rid>`.

## Common problems

- Port `4200` or `5113` is occupied: stop the existing process before running
  `dev`; the checked-in development proxy expects those ports.
- Frontend tests cannot find Chrome: install Chrome/Chromium or set
  `CHROME_BIN` to its executable.
- The data directory is read-only: choose a writable directory with
  `MYNOVELBUILDER_DATA_DIR` or `--data-dir`.
- SQLite reports a locked database: close other MyNovelBuilder backend or
  desktop processes using the same data directory.
- A localhost TTS server is unavailable: start that separate server and verify
  its configured port; see the [Docker guide](docker.md) for container URLs.
- Generated output is stale: remove only the ignored `artifacts/` directory
  and rerun the relevant task. Do not delete an application-data directory as
  a cache-clearing step.

Run `node scripts/tasks.mjs --help` for the complete task list.
