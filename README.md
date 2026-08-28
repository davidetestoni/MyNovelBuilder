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
