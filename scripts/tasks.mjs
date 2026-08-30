#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { createReadStream, existsSync } from 'node:fs';
import { rm, writeFile } from 'node:fs/promises';
import { basename, dirname, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawn } from 'node:child_process';

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const repositoryRoot = resolve(scriptDirectory, '..');
const frontendDirectory = resolve(
  repositoryRoot,
  'src/frontend/my-novel-builder',
);
const solution = resolve(
  repositoryRoot,
  'src/backend/MyNovelBuilder/MyNovelBuilder.sln',
);
const webApiProject = resolve(
  repositoryRoot,
  'src/backend/MyNovelBuilder/MyNovelBuilder.WebApi/MyNovelBuilder.WebApi.csproj',
);
const publishDirectory = resolve(repositoryRoot, 'artifacts/publish/web');
const desktopDevelopmentRoot = resolve(repositoryRoot, 'artifacts/desktop/dev');
const desktopPackageRoot = resolve(repositoryRoot, 'artifacts/desktop/packages');
const desktopShellTest = resolve(
  repositoryRoot,
  'scripts/tests/electron-custom-main.test.cjs',
);
const supportedDesktopRuntimes = new Set([
  'win-x64',
  'win-arm64',
  'osx-x64',
  'osx-arm64',
  'linux-x64',
  'linux-arm64',
]);

const help = `MyNovelBuilder repository tasks

Usage:
  node scripts/tasks.mjs <command> [arguments]

Commands:
  restore      Install locked frontend and backend dependencies
  test         Restore dependencies and run all backend and frontend tests
  build        Restore dependencies and build the frontend and backend in Release mode
  publish-web  Create a runnable ASP.NET Core + Angular publish in artifacts/publish/web
  dev          Restore dependencies and run the backend and Angular development servers
  desktop-dev  Build and run the application in an Electron window
  package-desktop <rid>
               Build unsigned desktop packages for win/osx/linux, x64 or arm64
  help         Show this help
`;

function displayPath(path) {
  const localPath = relative(repositoryRoot, path);
  return localPath.length === 0 ? '.' : localPath;
}

function quoteArgument(argument) {
  return /[\s"]/u.test(argument)
    ? `"${argument.replaceAll('"', '\\"')}"`
    : argument;
}

function start(command, arguments_, options = {}) {
  const cwd = options.cwd ?? repositoryRoot;
  const renderedCommand = [command, ...arguments_]
    .map(quoteArgument)
    .join(' ');

  console.log(`\n> [${displayPath(cwd)}] ${renderedCommand}`);

  const child = spawn(command, arguments_, {
    cwd,
    env: options.env ?? process.env,
    stdio: 'inherit',
    detached: options.detached ?? false,
    shell: options.shell ?? false,
    windowsHide: false,
  });

  const completion = new Promise((resolveCompletion) => {
    child.once('error', (error) => {
      const detail = error.code === 'ENOENT'
        ? `Required command '${command}' was not found on PATH.`
        : error.message;
      resolveCompletion({ code: 1, signal: null, error: new Error(detail) });
    });
    child.once('exit', (code, signal) => {
      resolveCompletion({ code, signal, error: null });
    });
  });

  return { child, completion, renderedCommand };
}

async function run(command, arguments_, options = {}) {
  const processInfo = start(command, arguments_, options);
  const result = await processInfo.completion;

  if (result.error) {
    throw result.error;
  }
  if (result.code !== 0) {
    const reason = result.signal
      ? `signal ${result.signal}`
      : `exit code ${result.code}`;
    throw new Error(`${processInfo.renderedCommand} failed with ${reason}.`);
  }
}

function startNpm(arguments_, options = {}) {
  return start('npm', arguments_, {
    ...options,
    shell: process.platform === 'win32',
  });
}

async function runNpm(arguments_, options = {}) {
  await run('npm', arguments_, {
    ...options,
    shell: process.platform === 'win32',
  });
}

async function restoreDependencies() {
  await runNpm(['ci'], { cwd: frontendDirectory });
  await run('dotnet', ['restore', solution, '--locked-mode']);
}

function frontendTestEnvironment() {
  if (process.env.CHROME_BIN || process.platform !== 'linux') {
    return process.env;
  }

  // Karma looks for Google Chrome by default. These cover common Chromium
  // installations used by Linux developer machines and CI images.
  const chromiumCandidates = [
    '/snap/bin/chromium',
    '/usr/bin/chromium',
    '/usr/bin/chromium-browser',
    '/usr/bin/google-chrome',
    '/usr/bin/google-chrome-stable',
  ];
  const chromium = chromiumCandidates.find((candidate) => existsSync(candidate));

  return chromium ? { ...process.env, CHROME_BIN: chromium } : process.env;
}

function dotnetWatchEnvironment() {
  if (
    process.env.DOTNET_USE_POLLING_FILE_WATCHER
    || process.platform !== 'linux'
  ) {
    return process.env;
  }

  // Polling avoids failures on Linux systems whose inotify instance limit is
  // already consumed by editors, browsers, or other development servers.
  return { ...process.env, DOTNET_USE_POLLING_FILE_WATCHER: '1' };
}

function currentDesktopRuntime() {
  const platform = {
    darwin: 'osx',
    linux: 'linux',
    win32: 'win',
  }[process.platform];
  const architecture = {
    arm64: 'arm64',
    x64: 'x64',
  }[process.arch];

  if (!platform || !architecture) {
    throw new Error(
      `Electron development is not supported on ${process.platform}-${process.arch}.`,
    );
  }

  return `${platform}-${architecture}`;
}

function hostDesktopPlatform() {
  const platform = {
    darwin: 'osx',
    linux: 'linux',
    win32: 'win',
  }[process.platform];

  if (!platform) {
    throw new Error(`Desktop packaging is not supported on ${process.platform}.`);
  }

  return platform;
}

function validateDesktopPackageRuntime(runtime) {
  if (!supportedDesktopRuntimes.has(runtime)) {
    throw new Error(
      `Unsupported desktop runtime '${runtime}'. Expected one of: ${[
        ...supportedDesktopRuntimes,
      ].join(', ')}.`,
    );
  }

  const targetPlatform = runtime.slice(0, runtime.lastIndexOf('-'));
  const hostPlatform = hostDesktopPlatform();
  if (targetPlatform !== hostPlatform) {
    throw new Error(
      `Cannot package ${runtime} on ${process.platform}. Electron packages must `
      + `be built on their target OS; choose a ${hostPlatform}-x64 or `
      + `${hostPlatform}-arm64 runtime on this machine.`,
    );
  }
}

async function writeSha256Checksum(filePath) {
  const hash = createHash('sha256');
  for await (const chunk of createReadStream(filePath)) {
    hash.update(chunk);
  }

  const checksumPath = `${filePath}.sha256`;
  await writeFile(
    checksumPath,
    `${hash.digest('hex')}  ${basename(filePath)}\n`,
    'utf8',
  );
  return checksumPath;
}

async function testApplication() {
  await restoreDependencies();
  await run('node', ['--test', desktopShellTest]);
  await run('dotnet', [
    'test',
    solution,
    '--configuration',
    'Release',
    '--no-restore',
  ]);
  await runNpm(
    ['test', '--', '--watch=false', '--browsers=ChromeHeadless'],
    { cwd: frontendDirectory, env: frontendTestEnvironment() },
  );
}

async function buildApplication() {
  await restoreDependencies();
  await runNpm(['run', 'build'], { cwd: frontendDirectory });
  await run('dotnet', [
    'build',
    solution,
    '--configuration',
    'Release',
    '--no-restore',
  ]);
}

async function publishWebApplication() {
  await run('dotnet', ['restore', webApiProject, '--locked-mode']);
  await rm(publishDirectory, { recursive: true, force: true });
  await run('dotnet', [
    'publish',
    webApiProject,
    '--configuration',
    'Release',
    '--no-restore',
    '--output',
    publishDirectory,
    '/p:UseAppHost=false',
  ]);

  const requiredFiles = [
    'MyNovelBuilder.WebApi.dll',
    'wwwroot/index.html',
    'Seed/prompts.json',
  ];
  const missingFiles = requiredFiles.filter(
    (path) => !existsSync(resolve(publishDirectory, path)),
  );
  if (missingFiles.length > 0) {
    throw new Error(
      `Published application is missing: ${missingFiles.join(', ')}.`,
    );
  }
  if (existsSync(resolve(publishDirectory, 'AppData'))) {
    throw new Error(
      'Published application contains local AppData; refusing unsafe output.',
    );
  }

  console.log(`\nPublished application: ${displayPath(publishDirectory)}`);
}

async function packageDesktopApplication(runtime) {
  validateDesktopPackageRuntime(runtime);

  const outputDirectory = resolve(desktopPackageRoot, runtime);
  const publishProfile = `Desktop-${runtime}`;
  const architecture = runtime.endsWith('-arm64') ? 'arm64' : 'x64';
  const packageEnvironment = {
    ...process.env,
    CSC_IDENTITY_AUTO_DISCOVERY: 'false',
    GH_TOKEN: '',
    GITHUB_TOKEN: '',
    MNB_DESKTOP_ARCH: architecture,
  };
  const desktopProperties = [
    '/p:ElectronDesktopBuild=true',
    `/p:PublishProfile=${publishProfile}`,
  ];

  await rm(outputDirectory, { recursive: true, force: true });
  await run('dotnet', [
    'restore',
    webApiProject,
    '--locked-mode',
    ...desktopProperties,
  ], { env: packageEnvironment });
  await run('dotnet', [
    'publish',
    webApiProject,
    '--configuration',
    'Release',
    '--no-restore',
    '--runtime',
    runtime,
    ...desktopProperties,
  ], { env: packageEnvironment });

  const expectedPackages = runtime.startsWith('win-')
    ? [`MyNovelBuilder-Windows-${architecture}-Setup.exe`]
    : runtime.startsWith('osx-')
      ? [`MyNovelBuilder-macOS-${architecture}.dmg`]
      : [
        `MyNovelBuilder-Linux-${architecture}.AppImage`,
        `MyNovelBuilder-Linux-${architecture}.deb`,
      ];
  const missingPackages = expectedPackages.filter(
    (file) => !existsSync(resolve(outputDirectory, file)),
  );
  if (missingPackages.length > 0) {
    throw new Error(
      `Desktop packaging completed without producing: ${missingPackages.join(', ')}.`,
    );
  }

  const checksumFiles = [];
  for (const file of expectedPackages) {
    checksumFiles.push(await writeSha256Checksum(resolve(outputDirectory, file)));
  }

  const unpackedDirectory = runtime.startsWith('win-')
    ? `${runtime === 'win-arm64' ? 'win-arm64' : 'win'}-unpacked`
    : runtime.startsWith('osx-')
      ? runtime === 'osx-arm64' ? 'mac-arm64' : 'mac'
      : `${runtime === 'linux-arm64' ? 'linux-arm64' : 'linux'}-unpacked`;
  await rm(resolve(outputDirectory, unpackedDirectory), {
    recursive: true,
    force: true,
  });
  await rm(resolve(outputDirectory, 'builder-debug.yml'), { force: true });

  // electron-builder stages another complete copy under the project's bin
  // tree. The distributables are self-contained, so retain only those outputs.
  await rm(
    resolve(dirname(webApiProject), 'bin', 'Release', 'net10.0', runtime),
    { recursive: true, force: true },
  );

  console.log(`\nDesktop packages: ${displayPath(outputDirectory)}`);
  for (const file of [
    ...expectedPackages,
    ...checksumFiles.map((checksum) => basename(checksum)),
  ]) {
    console.log(`  ${file}`);
  }
}

async function runDesktopDevelopment() {
  const runtime = currentDesktopRuntime();
  const outputDirectory = resolve(desktopDevelopmentRoot, runtime);
  const developmentDataDirectory = resolve(
    dirname(webApiProject),
    'AppData',
  );
  const executable = resolve(
    outputDirectory,
    process.platform === 'win32'
      ? 'MyNovelBuilder.WebApi.exe'
      : 'MyNovelBuilder.WebApi',
  );
  const electronExecutable = process.platform === 'win32'
    ? resolve(outputDirectory, '.electron/node_modules/electron/dist/electron.exe')
    : process.platform === 'darwin'
      ? resolve(
        outputDirectory,
        '.electron/node_modules/electron/dist/Electron.app/Contents/MacOS/Electron',
      )
      : resolve(outputDirectory, '.electron/node_modules/electron/dist/electron');
  const desktopProperties = [
    '/p:ElectronDesktopBuild=true',
    '/p:ElectronDevelopmentBuild=true',
  ];

  await runNpm(['ci'], { cwd: frontendDirectory });
  await runNpm(['run', 'build'], { cwd: frontendDirectory });
  if (existsSync(outputDirectory) && !existsSync(electronExecutable)) {
    // Recover from a previously interrupted Electron npm installation. A
    // complete host is kept as a local cache because it is several hundred MB.
    await rm(outputDirectory, { recursive: true, force: true });
  }
  await run('dotnet', [
    'restore',
    webApiProject,
    '--locked-mode',
    ...desktopProperties,
  ]);
  await run('dotnet', [
    'build',
    webApiProject,
    '--configuration',
    'Debug',
    '--no-restore',
    '--runtime',
    runtime,
    '--output',
    outputDirectory,
    ...desktopProperties,
  ]);

  const requiredFiles = [
    executable,
    electronExecutable,
    resolve(outputDirectory, 'wwwroot/index.html'),
    resolve(outputDirectory, '.electron/package.json'),
  ];
  const missingFiles = requiredFiles.filter((path) => !existsSync(path));
  if (missingFiles.length > 0) {
    throw new Error(
      `Desktop development build is missing: ${missingFiles
        .map(displayPath)
        .join(', ')}.`,
    );
  }

  console.log(`\nStarting the ${runtime} Electron application.`);
  if (process.platform === 'linux') {
    console.log(
      'The unpackaged Linux host uses --no-sandbox; packaged releases must not.',
    );
  }
  console.log('Close its window or press Ctrl+C to stop it.');
  const electronArguments = ['-unpackeddotnet'];
  if (process.platform === 'linux') {
    // Ubuntu AppArmor blocks the user-namespace fallback and an unpackaged
    // npm install cannot provide a root-owned setuid Chromium helper. This is
    // strictly a local development escape hatch, never a packaging setting.
    electronArguments.push('--no-sandbox');
  }
  const detached = process.platform !== 'win32';
  const desktop = start(executable, electronArguments, {
    cwd: outputDirectory,
    detached,
    env: {
      ...process.env,
      ASPNETCORE_ENVIRONMENT:
        process.env.ASPNETCORE_ENVIRONMENT ?? 'Development',
      MYNOVELBUILDER_DATA_DIR:
        process.env.MYNOVELBUILDER_DATA_DIR ?? developmentDataDirectory,
    },
  });

  let resolveSignal;
  const interrupted = new Promise((resolveInterruption) => {
    resolveSignal = resolveInterruption;
  });
  const onInterrupt = () => resolveSignal('SIGINT');
  const onTerminate = () => resolveSignal('SIGTERM');
  process.once('SIGINT', onInterrupt);
  process.once('SIGTERM', onTerminate);

  const outcome = await Promise.race([
    desktop.completion.then((result) => ({ type: 'exit', result })),
    interrupted.then((signal) => ({ type: 'signal', signal })),
  ]);
  process.removeListener('SIGINT', onInterrupt);
  process.removeListener('SIGTERM', onTerminate);

  if (outcome.type === 'signal') {
    await terminateDevelopmentProcess(desktop.child, outcome.signal);
    await desktop.completion;
    process.exitCode = outcome.signal === 'SIGINT' ? 130 : 143;
    return;
  }
  if (outcome.result.error) {
    throw outcome.result.error;
  }
  if (outcome.result.code !== 0) {
    const reason = outcome.result.signal
      ? `signal ${outcome.result.signal}`
      : `exit code ${outcome.result.code}`;
    throw new Error(`${desktop.renderedCommand} failed with ${reason}.`);
  }
}

async function terminateDevelopmentProcess(child, signal) {
  if (child.exitCode !== null || child.signalCode !== null) {
    return;
  }

  if (process.platform === 'win32') {
    await new Promise((resolveTermination) => {
      const terminator = spawn(
        'taskkill',
        ['/pid', String(child.pid), '/t', '/f'],
        { stdio: 'ignore', windowsHide: true },
      );
      terminator.once('error', resolveTermination);
      terminator.once('exit', resolveTermination);
    });
    return;
  }

  try {
    process.kill(-child.pid, signal);
  } catch (error) {
    if (error.code !== 'ESRCH') {
      throw error;
    }
  }
}

async function runDevelopmentServers() {
  await restoreDependencies();

  console.log('\nStarting the API at http://localhost:5113 and Angular at http://localhost:4200.');
  console.log('Press Ctrl+C to stop both processes.');

  const detached = process.platform !== 'win32';
  const backend = start(
    'dotnet',
    [
      'watch',
      'run',
      '--project',
      webApiProject,
      '--launch-profile',
      'http',
      '--no-restore',
    ],
    { detached, env: dotnetWatchEnvironment() },
  );
  const frontend = startNpm(['start'], {
    cwd: frontendDirectory,
    detached,
  });
  const processes = [backend, frontend];

  let resolveSignal;
  const interrupted = new Promise((resolveInterruption) => {
    resolveSignal = resolveInterruption;
  });
  const onInterrupt = () => resolveSignal({ type: 'signal', signal: 'SIGINT' });
  const onTerminate = () => resolveSignal({ type: 'signal', signal: 'SIGTERM' });
  process.once('SIGINT', onInterrupt);
  process.once('SIGTERM', onTerminate);

  const exited = Promise.race(
    processes.map(async (processInfo) => ({
      type: 'exit',
      processInfo,
      result: await processInfo.completion,
    })),
  );
  const outcome = await Promise.race([interrupted, exited]);

  process.removeListener('SIGINT', onInterrupt);
  process.removeListener('SIGTERM', onTerminate);

  const shutdownSignal = outcome.type === 'signal' ? outcome.signal : 'SIGTERM';
  await Promise.all(
    processes.map(({ child }) => terminateDevelopmentProcess(child, shutdownSignal)),
  );
  await Promise.all(processes.map(({ completion }) => completion));

  if (outcome.type === 'signal') {
    process.exitCode = outcome.signal === 'SIGINT' ? 130 : 143;
    return;
  }

  if (outcome.result.error) {
    throw outcome.result.error;
  }
  const reason = outcome.result.signal
    ? `signal ${outcome.result.signal}`
    : `exit code ${outcome.result.code}`;
  throw new Error(
    `${outcome.processInfo.renderedCommand} exited unexpectedly with ${reason}.`,
  );
}

async function main() {
  const [command, ...extraArguments] = process.argv.slice(2);

  if (!command || command === 'help' || command === '--help' || command === '-h') {
    console.log(help);
    return;
  }
  const tasks = {
    restore: restoreDependencies,
    test: testApplication,
    build: buildApplication,
    'publish-web': publishWebApplication,
    dev: runDevelopmentServers,
    'desktop-dev': runDesktopDevelopment,
    'package-desktop': packageDesktopApplication,
  };
  const task = tasks[command];
  if (!task) {
    throw new Error(`Unknown command '${command}'. Run with --help to list commands.`);
  }

  if (command === 'package-desktop') {
    if (extraArguments.length !== 1) {
      throw new Error(
        'package-desktop requires exactly one runtime, for example linux-x64.',
      );
    }
  } else if (extraArguments.length > 0) {
    throw new Error(`Unexpected arguments: ${extraArguments.join(' ')}`);
  }

  await task(...extraArguments);
}

main().catch((error) => {
  console.error(`\nTask failed: ${error.message}`);
  process.exitCode = 1;
});
