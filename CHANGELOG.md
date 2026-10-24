# Changelog

## [Unreleased]

## [0.2.0] - 2026-10-24

### Added

- Audiobook generation from Novel Settings, with narrator/immersive modes,
  per-export settings, voice preview, automatic reuse estimates, saved progress,
  cancellation/retry, and ZIP downloads containing chapter WAVs.
- Reusable audiobook audio caching and startup cleanup of abandoned temporary
  files and expired generated files. The default age limit is 90 days,
  configurable with `GeneratedAudioRetentionDays`; the latest successful
  download for each novel, saved job history, and referenced speech are preserved.
- KittenTTS 2 support, including built-in voices and custom voice samples with
  reference transcripts.
- In-browser transcription of voice samples using Whistle, with editable
  transcripts and support for English, French, German, Italian, and Spanish.
  Transcription runs locally without an external speech-to-text service.
- Stop controls for text generation in chat, prose/RPG tools, story-event and
  story-suggestion dialogs, and novel translation.
- Custom OpenRouter voice IDs in Integrations, compendium character voices,
  and audiobook narration, including models without an advertised voice list.

### Changed

- All TTS providers keep synthesis sequential through cancellation and closed
  playback streams. Cancellation waits for an active provider operation to
  finish before another starts.
- Compendium galleries now display media in a stable order by file timestamp.

### Fixed

- OpenRouter speech playback now decodes streamed MP3 using its embedded audio
  format instead of assuming a fixed raw PCM format. Provider error messages
  include available details about invalid model/voice combinations.
- Integration balance checks refresh when providers change and only query
  configured providers currently selected for speech, images, or video.

### Migration notes

- No database migration or manual data conversion is required for audiobook
  jobs. Generated files and progress are stored in the application data folder.
- When building from an existing source checkout, install Git LFS and run
  `git lfs pull` to fetch the browser transcription runtime and model assets.
  Desktop and Docker packages include these assets.

## [0.1.0] - 2026-09-03

### Added

- First version of the application.
- 15 editable starter prompts covering every supported prompt category.
- An example novel to showcase the application's capabilities.
- A production web build in which ASP.NET Core serves both the Angular app and
  the API from one process.
- A Docker image layout and Compose configuration with persistent application
  data and support for reaching TTS servers running on the host.

### Changed

- Application data now defaults to the conventional per-user data directory on
  Windows, macOS, and Linux instead of living beside the application binaries.

### Migration notes

- Existing databases are migrated automatically at startup after a validated,
  timestamped backup is created.
