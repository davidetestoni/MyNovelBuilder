'use strict';

const assert = require('node:assert/strict');
const test = require('node:test');

const {
  attachNavigationGuards,
  configureDesktopIdentity,
  isExternalBrowserUrl,
  isLoopbackApplicationUrl,
} = require('../../src/backend/MyNovelBuilder/MyNovelBuilder.WebApi/Desktop/electron-custom-main.cjs');

test('matches the installed Linux desktop entry before startup', () => {
  const desktopNames = [];
  const app = {
    setDesktopName: (name) => desktopNames.push(name),
  };

  configureDesktopIdentity(app, 'linux');
  configureDesktopIdentity(app, 'win32');
  configureDesktopIdentity(app, 'darwin');

  assert.deepEqual(desktopNames, ['MyNovelBuilder.desktop']);
});

test('recognizes only HTTP loopback application URLs', () => {
  assert.equal(isLoopbackApplicationUrl('http://127.0.0.1:43210/novels'), true);
  assert.equal(isLoopbackApplicationUrl('http://localhost:43210/'), true);
  assert.equal(isLoopbackApplicationUrl('http://[::1]:43210/'), true);
  assert.equal(isLoopbackApplicationUrl('https://example.com/'), false);
  assert.equal(isLoopbackApplicationUrl('file:///tmp/index.html'), false);
});

test('allows only HTTPS URLs to leave the application', () => {
  assert.equal(isExternalBrowserUrl('https://github.com/davidetestoni/MyNovelBuilder'), true);
  assert.equal(isExternalBrowserUrl('http://example.com/'), false);
  assert.equal(isExternalBrowserUrl('file:///tmp/untrusted'), false);
  assert.equal(isExternalBrowserUrl('javascript:alert(1)'), false);
  assert.equal(isExternalBrowserUrl('not a URL'), false);
});

test('locks navigation to the first loopback origin', async () => {
  const listeners = new Map();
  let windowOpenHandler;
  const loadedUrls = [];
  const openedUrls = [];
  const webContents = {
    on: (name, handler) => listeners.set(name, handler),
    setWindowOpenHandler: (handler) => {
      windowOpenHandler = handler;
    },
    loadURL: async (url) => {
      loadedUrls.push(url);
    },
  };
  const shell = {
    openExternal: async (url) => {
      openedUrls.push(url);
    },
  };

  attachNavigationGuards(webContents, shell);

  const initialEvent = { prevented: false, preventDefault() { this.prevented = true; } };
  listeners.get('will-navigate')(initialEvent, 'http://127.0.0.1:43210/');
  assert.equal(initialEvent.prevented, false);

  const internalEvent = { prevented: false, preventDefault() { this.prevented = true; } };
  listeners.get('will-navigate')(internalEvent, 'http://127.0.0.1:43210/novels');
  assert.equal(internalEvent.prevented, false);

  const otherPortEvent = { prevented: false, preventDefault() { this.prevented = true; } };
  listeners.get('will-navigate')(otherPortEvent, 'http://127.0.0.1:9999/');
  assert.equal(otherPortEvent.prevented, true);

  const externalEvent = { prevented: false, preventDefault() { this.prevented = true; } };
  listeners.get('will-navigate')(externalEvent, 'https://github.com/davidetestoni/MyNovelBuilder');
  assert.equal(externalEvent.prevented, true);

  assert.deepEqual(
    windowOpenHandler({ url: 'http://127.0.0.1:43210/about' }),
    { action: 'deny' },
  );
  assert.deepEqual(
    windowOpenHandler({ url: 'javascript:alert(1)' }),
    { action: 'deny' },
  );

  await new Promise((resolve) => setImmediate(resolve));
  assert.deepEqual(loadedUrls, ['http://127.0.0.1:43210/about']);
  assert.deepEqual(openedUrls, ['https://github.com/davidetestoni/MyNovelBuilder']);
});
