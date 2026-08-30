# Release conventions

MyNovelBuilder uses the root `VERSION` file as its canonical version and follows
[Semantic Versioning](https://semver.org/).

## Tags and release names

- Release tags use `vX.Y.Z`, for example `v0.1.0`.
- A release tag must point to a commit on `main` that has passed the required
  validation.
- While the application is in beta, GitHub Release titles use
  `MyNovelBuilder X.Y.Z Beta`.
- Beta releases on the single release channel are normal GitHub Releases, not
  GitHub prereleases. This keeps `releases/latest` and
  `releases/latest/download/...` links usable.

The `0.x` version and the visible Beta label communicate the application's
maturity. If prerelease SemVer identifiers such as `0.2.0-beta.1` are introduced
later, the release-link strategy must be updated because GitHub excludes
prereleases from its `latest` redirect.

## Changelog maintenance

User-visible changes belong in the root [changelog](../CHANGELOG.md) as they are
implemented. Keep them grouped under `Added`, `Changed`, `Fixed`, `Removed`,
`Security`, or `Migration notes`, using only the sections that apply.

For every release:

1. Confirm that `VERSION` contains the exact SemVer version being released.
2. Replace the pending changelog heading with `[X.Y.Z] - YYYY-MM-DD`.
3. Include a `Migration notes` section, explicitly saying `None` when no user
   action or compatibility concern exists.
4. Create a fresh `Unreleased` heading for subsequent work.
5. Use the matching `vX.Y.Z` tag and GitHub Release title.

Do not put internal refactors, test-only changes, or routine dependency churn in
the changelog unless they affect users, compatibility, security, or installation.
