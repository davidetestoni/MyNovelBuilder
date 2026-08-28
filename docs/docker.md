# Run MyNovelBuilder with Docker

Docker runs the complete application as one container: ASP.NET Core serves the
Angular interface and the API on the same port.

## Requirements

- Docker Engine or Docker Desktop
- Docker Compose v2 (`docker compose`)

## Start the application

Download `compose.yaml`, open a terminal in the directory containing it, and
run:

```shell
docker compose up -d
```

Open <http://localhost:5113>. The supplied Compose configuration binds only to
the local computer, so other devices on the network cannot connect by default.

Check the container status or view its logs with:

```shell
docker compose ps
docker compose logs --follow app
```

Stop and remove the container with:

```shell
docker compose down
```

This does not delete your application data.

## Persistent data

The Compose configuration stores `/data` in the named
`mynovelbuilder-data` volume. This includes the SQLite database, integration
settings, API keys, media, generated audio, chats, and other application-owned
files.

The volume is independent of the replaceable container. Normal commands such
as `docker compose down`, `docker compose pull`, and `docker compose up -d`
preserve it.

Do not run the following command unless you intend to permanently delete your
MyNovelBuilder data:

```shell
docker compose down --volumes
```

Integration credentials are stored as plaintext inside
`/data/integrations.json`. Do not share that file, the Docker volume, or backups
of the volume. Never include `integrations.json` in screenshots, diagnostics,
support bundles, or release archives.

## Update

Pull the newest published image and recreate the container:

```shell
docker compose pull
docker compose up -d
```

The named volume is reused automatically. Database migrations run when the new
container starts.

## Build the image locally

To test the current source instead of pulling the published image:

```shell
docker build --tag davidetestoni/my-novel-builder:latest .
docker compose up -d --pull never
```

## Connect to a TTS server on the host

Inside a container, `localhost` refers to the container itself, not the
computer running Docker. If a TTS server is running on the host, enter a URL
like this in the MyNovelBuilder integration settings:

```text
http://host.docker.internal:8000
```

Use the port exposed by that server. The supplied Compose configuration adds
the required `host-gateway` mapping for Linux; Docker Desktop provides the same
hostname on Windows and macOS.

The local TTS servers are separate applications and are not bundled, started,
secured, or supported automatically by MyNovelBuilder:

- [OmniVoice](https://github.com/davidetestoni/OmniVoice)
- [Qwen3-TTS](https://github.com/davidetestoni/Qwen3-TTS)
- [Chatterbox](https://github.com/davidetestoni/chatterbox)

## Local-only security boundary

MyNovelBuilder does not currently provide user authentication. Keep the
published port bound to `127.0.0.1` as supplied in `compose.yaml`. Do not expose
the container directly to the public internet or an untrusted local network.
