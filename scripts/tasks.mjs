#!/usr/bin/env node

import { existsSync } from 'node:fs';
import { rm } from 'node:fs/promises';
import { dirname, relative, resolve } from 'node:path';
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

const help = `MyNovelBuilder repository tasks

Usage:
  node scripts/tasks.mjs <command>

Commands:
  restore      Install locked frontend and backend dependencies
  test         Restore dependencies and run all backend and frontend tests
  build        Restore dependencies and build the frontend and backend in Release mode
  publish-web  Create a runnable ASP.NET Core + Angular publish in artifacts/publish/web
  dev          Restore dependencies and run the backend and Angular development servers
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

async function testApplication() {
  await restoreDependencies();
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
  if (extraArguments.length > 0) {
    throw new Error(`Unexpected arguments: ${extraArguments.join(' ')}`);
  }

  const tasks = {
    restore: restoreDependencies,
    test: testApplication,
    build: buildApplication,
    'publish-web': publishWebApplication,
    dev: runDevelopmentServers,
  };
  const task = tasks[command];
  if (!task) {
    throw new Error(`Unknown command '${command}'. Run with --help to list commands.`);
  }

  await task();
}

main().catch((error) => {
  console.error(`\nTask failed: ${error.message}`);
  process.exitCode = 1;
});
