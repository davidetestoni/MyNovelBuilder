# Changelog

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
