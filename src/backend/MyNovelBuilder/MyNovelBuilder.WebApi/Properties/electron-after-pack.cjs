const { chmod, rm } = require('node:fs/promises');
const path = require('node:path');

module.exports = async function afterPack(context) {
  const resourcesDirectory = context.packager.getResourcesDir(context.appOutDir);

  // Chromium falls back to this helper when unprivileged user namespaces are
  // restricted. DEB installs the staged tree as root, so preserving the setuid
  // bit makes that sandbox path usable. AppImage's FUSE mount cannot elevate
  // through setuid and must use the user-namespace sandbox instead.
  if (context.electronPlatformName === 'linux') {
    await chmod(path.join(context.appOutDir, 'chrome-sandbox'), 0o4755);
  }

  // The hook is copied into the staging tree so electron-builder can execute
  // it. It is build machinery, not part of the installed application.
  await rm(path.join(resourcesDirectory, 'bin', 'electron-after-pack.cjs'), {
    force: true,
  });

  // ElectronNET stages the complete build output rather than only files in
  // the ASP.NET Core publish manifest. Keep development-only configuration
  // out of every distributable even if it remains in an incremental bin tree.
  await rm(
    path.join(resourcesDirectory, 'bin', 'appsettings.Development.json'),
    { force: true },
  );
};
