# credfeto-dotnet-package-update

Command line .NET global tool that scans a folder of projects and updates NuGet package references to
the latest available version, for use in scripted or automated builds.

## Build Status

| Branch  | Status                                                |
| ------- | ----------------------------------------------------- |
| main    | [![Build: Pre-Release][pre-release-img]][pre-release] |
| release | [![Build: Release][release-img]][release]             |

[![NuGet][nuget-img]][nuget]
[![Licence][licence-img]][licence]

## Overview

`Credfeto.Package.Update` walks every project under a given folder, finds references to a package (or
a prefix of package ids), checks the configured NuGet feeds for a newer version, and rewrites the
project files in place. It is designed to be driven from a script or CI pipeline rather than used
interactively: it exits with a non-zero code when nothing was updated, and prints the ids and versions
of every package it changed so a calling script can act on the result.

## Quick Start

```shell
dotnet tool install --global Credfeto.Package.Update
dotnet updatepackages --folder D:\Source --package-id Credfeto.Extensions:prefix
```

## Installation

### Install as a global tool

```shell
dotnet tool install --global Credfeto.Package.Update
```

To update to the latest released version:

```shell
dotnet tool update --global Credfeto.Package.Update
```

### Install as a local tool

```shell
dotnet new tool-manifest
dotnet tool install Credfeto.Package.Update --local
```

To update to the latest released version:

```shell
dotnet tool update Credfeto.Package.Update --local
```

## Usage

### Command line options

| Option         | Short | Required | Description                                                                                                                    |
| -------------- | ----- | -------- | ------------------------------------------------------------------------------------------------------------------------------ |
| `--package-id` | `-p`  | Yes      | Package id to check for updates. Append `:prefix` to match every id starting with the given prefix.                            |
| `--folder`     | `-f`  | Yes      | Folder to scan for project files.                                                                                              |
| `--cache`      | `-c`  | No       | Path to a JSON file used to cache resolved package versions between runs. Loaded if it already exists and saved after the run. |
| `--source`     | `-s`  | No       | Additional NuGet feed URL to search. Repeatable.                                                                               |
| `--exclude`    | `-x`  | No       | Package id to exclude from the update. Append `:prefix` to exclude by prefix. Repeatable.                                      |

The tool returns a non-zero exit code both when the update fails and when no packages needed updating.

On success it prints one line per updated package in the format:

```text
::set-env name=PackageId::Version
```

e.g.

```text
::set-env name=Credfeto.Extensions.Configuration.Typed::1.2.3.4
::set-env name=Credfeto.Extensions.Caching::3.4.5.6
```

> **Note**: `::set-env` is a legacy GitHub Actions workflow command that GitHub Actions no longer
> honours (it was disabled in 2020). This output is currently only useful to scripts that parse it
> directly from stdout; see [issue #568][set-env-issue] for the tracked work to also write to
> `GITHUB_ENV`/`GITHUB_OUTPUT`.

### Examples

#### Update a specific package

```shell
dotnet updatepackages --folder D:\Source --package-id Credfeto.Extensions.Configuration.Typed
```

#### Update all packages that start with a prefix

```shell
dotnet updatepackages --folder D:\Source --package-id Credfeto.Extensions:prefix
```

#### Update all packages that start with a prefix, excluding specific packages

```shell
dotnet updatepackages --folder D:\Source --package-id Credfeto.Extensions:prefix --exclude Credfeto.Extensions.Configuration.Json Credfeto.Extensions.Configuration.Typed
```

#### Add an additional package source

```shell
dotnet updatepackages --folder D:\Source --package-id Credfeto.Extensions:prefix --source https://nuget.example.org/api/v3/index.json
```

#### Cache resolved versions between runs

```shell
dotnet updatepackages --folder D:\Source --package-id Credfeto.Extensions:prefix --cache D:\Source\.package-cache.json
```

## Documentation

See the [docs](docs/) folder for architecture and design notes.

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for release notes.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for how to contribute to this project.

## Security

See [SECURITY.md](SECURITY.md) for how to report security issues.

## Licence

This project is licensed under the MIT Licence, see [LICENSE](LICENSE) for details.

[licence-img]: https://img.shields.io/github/license/credfeto/credfeto-dotnet-package-update
[licence]: LICENSE
[nuget-img]: https://img.shields.io/nuget/v/Credfeto.Package.Update.svg
[nuget]: https://www.nuget.org/packages/Credfeto.Package.Update
[pre-release-img]: https://github.com/credfeto/UpdatePackages/actions/workflows/build-and-publish-pre-release.yml/badge.svg
[pre-release]: https://github.com/credfeto/UpdatePackages/actions/workflows/build-and-publish-pre-release.yml
[release-img]: https://github.com/credfeto/UpdatePackages/actions/workflows/build-and-publish-release.yml/badge.svg
[release]: https://github.com/credfeto/UpdatePackages/actions/workflows/build-and-publish-release.yml
[set-env-issue]: https://github.com/credfeto/credfeto-dotnet-package-update/issues/568
