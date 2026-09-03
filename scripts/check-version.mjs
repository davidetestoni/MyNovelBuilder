import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const repositoryRoot = path.dirname(
  fileURLToPath(new URL('../VERSION', import.meta.url)),
);
const version = readFileSync(path.join(repositoryRoot, 'VERSION'), 'utf8').trim();

const semverPattern =
  /^(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$/;
if (!semverPattern.test(version)) {
  throw new Error(`VERSION does not contain a valid SemVer version: ${version}`);
}

const frontendRoot = path.join(
  repositoryRoot,
  'src',
  'frontend',
  'my-novel-builder',
);
const packageJson = JSON.parse(
  readFileSync(path.join(frontendRoot, 'package.json'), 'utf8'),
);
const packageLock = JSON.parse(
  readFileSync(path.join(frontendRoot, 'package-lock.json'), 'utf8'),
);
const dockerfile = readFileSync(path.join(repositoryRoot, 'Dockerfile'), 'utf8');

const versions = [
  ['package.json', packageJson.version],
  ['package-lock.json', packageLock.version],
  ['package-lock.json root package', packageLock.packages?.['']?.version],
];

for (const [source, actualVersion] of versions) {
  if (actualVersion !== version) {
    throw new Error(
      `${source} has version ${actualVersion}; expected ${version}`,
    );
  }
}

if (!dockerfile.includes(`ARG APP_VERSION=${version}`)) {
  throw new Error(`Dockerfile APP_VERSION does not match VERSION (${version})`);
}

console.log(`Version metadata is synchronized at ${version}.`);
