# MyNovelBuilder documentation

MyNovelBuilder is a local workspace for drafting prose, organizing novel
structure and world-building material, and using bring-your-own-key
AI providers for text, images, video, and speech.

This is a beta passion project shaped around my own writing workflow.
It has no hosted service, user accounts, or telemetry.
Feature requests and contributions will be considered, but acceptance is not guaranteed.

## Choose how to run it

| Option | Best for | Guide |
| --- | --- | --- |
| Desktop package | Writers who want a normal application | [Desktop installation](desktop.md) |
| Docker | A reproducible local installation with persistent data | [Docker](docker.md) |
| Source | Contributors and developers | [Development setup](development.md) |

Desktop packages are unsigned.

## Use the application

- [First run](getting-started.md): understand the sample novel and take a
  short tour without configuring an AI provider.
- [Writing workflow](features/writing.md): create a novel, write and organize
  prose, plan story events, use RPG mode, and import or export a manuscript.
- [Compendia and AI tools](features/context-and-ai.md): manage reusable story
  context, customize prompts, chat about a novel, and review world-building
  proposals.

## Data and privacy at a glance

Novel data, media, prompts, chats, voices, and integration settings remain in
the selected local application-data directory. Integration credentials are currently
stored in plaintext in `integrations.json`; never share that file with anyone.

AI features are optional and use providers configured by the user. Requests
sent to a provider are subject to that provider's privacy terms and separate
usage charges. Non-AI writing and organization features work without an API
key.
