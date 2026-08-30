# MyNovelBuilder

An AI-powered workspace for novelists to write prose, build worlds, and brainstorm.

## Features

- **Prose Editor**: A clean writing environment with integrated image support.
- **Compendia**: Structured world-building for characters, locations, and lore.
- **AI Chat**: Dedicated brainstorming interface using OpenRouter.
- **Generative Tools**: Built-in support for text generation, AI image creation, and TTS for prose playback.
- **Prompt Library**: Save and manage prompts for consistent creative assistance.
- **Integrations**: Centralized management for AI providers and API keys.

## Run with Docker

From a directory containing `compose.yaml`:

```shell
docker compose up -d
```

Open <http://localhost:5113>. Your novels and settings are kept in a Docker
volume and survive container replacement and upgrades.

See the [Docker guide](docs/docker.md) for updates, local image builds,
persistent-data details, and connecting to TTS servers running on the host.

## Release history

See the [changelog](CHANGELOG.md) for user-visible changes. Maintainer-facing
versioning and release conventions are documented in the
[release guide](docs/releases.md).

## Repository tasks

The cross-platform task runner keeps local development and future CI on the
same commands:

```shell
node scripts/tasks.mjs restore
node scripts/tasks.mjs test
node scripts/tasks.mjs build
node scripts/tasks.mjs publish-web
node scripts/tasks.mjs dev
```

Run `node scripts/tasks.mjs --help` for details. `publish-web` creates the
complete ASP.NET Core and Angular application under `artifacts/publish/web`;
generated files in `artifacts` are ignored by Git.
