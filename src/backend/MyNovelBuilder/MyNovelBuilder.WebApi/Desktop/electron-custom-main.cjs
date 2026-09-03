'use strict';

const LINUX_DESKTOP_NAME = 'MyNovelBuilder.desktop';
const LOOPBACK_HOSTS = new Set(['127.0.0.1', '::1', '[::1]', 'localhost']);

function configureDesktopIdentity(app, platform = process.platform) {
  if (platform === 'linux') {
    app.setDesktopName(LINUX_DESKTOP_NAME);
  }
}

function parseUrl(value) {
  try {
    return new URL(value);
  } catch {
    return null;
  }
}

function isLoopbackApplicationUrl(value) {
  const url = parseUrl(value);
  return (
    url !== null &&
    url.protocol === 'http:' &&
    LOOPBACK_HOSTS.has(url.hostname.toLowerCase())
  );
}

function isExternalBrowserUrl(value) {
  const url = parseUrl(value);
  return url !== null && url.protocol === 'https:';
}

function attachNavigationGuards(webContents, shell, logger = console) {
  let applicationOrigin = null;

  function isApplicationUrl(value) {
    const url = parseUrl(value);
    if (url === null) {
      return false;
    }

    if (applicationOrigin === null) {
      if (!isLoopbackApplicationUrl(value)) {
        return false;
      }

      applicationOrigin = url.origin;
    }

    return url.origin === applicationOrigin;
  }

  function openExternal(value) {
    if (!isExternalBrowserUrl(value)) {
      return;
    }

    void shell.openExternal(value).catch((error) => {
      logger.error(`Unable to open external URL: ${value}`, error);
    });
  }

  function guardNavigation(event, url) {
    if (isApplicationUrl(url)) {
      return;
    }

    event.preventDefault();
    openExternal(url);
  }

  webContents.on('will-navigate', guardNavigation);
  webContents.on('will-redirect', guardNavigation);
  webContents.setWindowOpenHandler(({ url }) => {
    if (isApplicationUrl(url)) {
      void webContents.loadURL(url);
    } else {
      openExternal(url);
    }

    return { action: 'deny' };
  });
}

function onStartup() {
  const { app, shell } = require('electron');
  configureDesktopIdentity(app);
  app.on('web-contents-created', (_event, webContents) => {
    attachNavigationGuards(webContents, shell);
  });

  return true;
}

module.exports = {
  attachNavigationGuards,
  configureDesktopIdentity,
  isExternalBrowserUrl,
  isLoopbackApplicationUrl,
  onStartup,
};
