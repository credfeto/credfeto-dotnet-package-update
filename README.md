# credfeto-dotnet-package-update

Command line tool for updating nuget packages across a solution/folder which can be scripted as part of an automated
build.

## Build Status

| Branch  | Status                                                                                                                                                                                                                            |
|---------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| main    | [![Build: Pre-Release](https://github.com/credfeto/UpdatePackages/actions/workflows/build-and-publish-pre-release.yml/badge.svg)](https://github.com/credfeto/UpdatePackages/actions/workflows/build-and-publish-pre-release.yml) |
| release | [![Build: Release](https://github.com/credfeto/UpdatePackages/actions/workflows/build-and-publish-release.yml/badge.svg)](https://github.com/credfeto/UpdatePackages/actions/workflows/build-and-publish-release.yml)             |

## Release Notes

See [CHANGELOG.md](CHANGELOG.md) for release notes.

## Installation

### Install as a global tool

```shell
dotnet tool install Credfeto.Package.Update
```

To update to latest released version

```shell
dotnet tool update Credfeto.Package.Update
```

### Install as a local tool

```shell
dotnet new tool-manifest
dotnet tool install Credfeto.Package.Update --local
```

To update to latest released version

```shell
dotnet tool update Credfeto.Package.Update --local
```

## Usage

### Updating packages

Note the tool returns a non-zero exit code if any packages are updated.

Also provides in the output the packages that were updated in the format below so that scripts can read the output and
take action.

```text
::set-env name=PackageId::Version
```

e.g:

```text
::set-env name=Credfeto.Extensions.Configuration.Typed::1.2.3.4
::set-env name=Credfeto.Extensions.Caching::3.4.5.6
```

#### Update a specific package

```shell
dotnet updatepackages --folder D:\Source --package-id Credfeto.Extensions.Configuration.Typed
```

#### Update all packages that start with a prefix

```shell
dotnet updatepackages --folder D:\Source --package-id Credfeto.Extensions:prefix
```

#### Update all packages that start with a prefix, excluding a specific packages

```shell
dotnet updatepackages --folder D:\Source --package-id Credfeto.Extensions:prefix --exclude Credfeto.Extensions.Configuration.Json Credfeto.Extensions.Configuration.Typed
```

#### Add an additional package source

```shell
dotnet updatepackages --folder D:\Source --package-id Credfeto.Extensions:prefix --source https://nuget.example.org/api/v3/index.json
```

## Contributors

<!-- ALL-CONTRIBUTORS-LIST:START - Do not remove or modify this section -->
<!-- prettier-ignore-start -->
<!-- markdownlint-disable -->

<!-- markdownlint-restore -->
<!-- prettier-ignore-end -->

<!-- ALL-CONTRIBUTORS-LIST:END -->
