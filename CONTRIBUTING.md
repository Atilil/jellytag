# Contributing

Issues and pull requests are welcome. Every pull request is linted, built and
tested by GitHub Actions.

## Scope

JellyTag shows what Jellyfin already knows about a media item: what it reads
from the files (resolution, HDR, codecs, audio, languages) and the metadata it
stores for every item (runtime, ratings...). Features that depend on a
country-specific system, such as national age ratings, are out of scope: they
would need one badge set and one parser per country.

If you are unsure whether an idea fits, open an issue before writing code.

## Development environment

The dev shell is provided by Nix (`nix develop`, or direnv with `use flake`).
Without Nix, install the .NET SDK version pinned in `global.json` and
[just](https://just.systems).

```bash
just build          # compile the plugin
just test           # run the unit tests
just integration    # run the integration tests in an offline Jellyfin (podman)
just lint           # run the pre-commit checks (needs the Nix dev shell)
just dev-up         # start a local Jellyfin 12.4 instance (podman)
just dev-media      # generate test movies (resolutions, audio, languages)
just dev-deploy     # build, install into the local instance, restart it
just package X.Y.Z  # build the release zip and notes into artifacts/
```

The local instance listens on <http://localhost:8097>.

## Commits and releases

Commit messages follow
[Conventional Commits](https://www.conventionalcommits.org) (`feat:`, `fix:`,
`perf:`…): the release notes are generated from them. Pull requests are squash
merged, so the PR title must follow it too; CI checks it. Releases are built by
GitHub Actions when a `vX.Y.Z` tag is pushed.
