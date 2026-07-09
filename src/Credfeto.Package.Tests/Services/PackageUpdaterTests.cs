using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Package.Services;
using FunFair.Test.Common;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NuGet.Versioning;
using Xunit;

namespace Credfeto.Package.Tests.Services;

public sealed class PackageUpdaterTests : LoggingFolderCleanupTestBase
{
    private static readonly IReadOnlyList<string> NO_PACKAGE_SOURCES = [];

    public PackageUpdaterTests(ITestOutputHelper output)
        : base(output) { }

    private PackageUpdater CreateUpdater(
        IProjectLoader projectLoader,
        IPackageRegistry packageRegistry,
        IPackageCache packageCache
    )
    {
        ILogger<PackageUpdater> logger = this.GetTypedLogger<PackageUpdater>();

        return new PackageUpdater(
            projectLoader: projectLoader,
            packageRegistry: packageRegistry,
            packageCache: packageCache,
            logger: logger
        );
    }

    private static PackageUpdateConfiguration MakeConfig(string packageId, bool prefix = false)
    {
        return new PackageUpdateConfiguration(
            PackageMatch: new PackageMatch(PackageId: packageId, Prefix: prefix),
            ExcludedPackages: []
        );
    }

    private Task CreateDummyCsprojAsync(string name = "Test.csproj")
    {
        string path = Path.Combine(this.TempFolder, name);

        return File.WriteAllTextAsync(path: path, contents: "<Project />", cancellationToken: this.CancellationToken());
    }

    [Fact]
    public async Task UpdateAsync_WithNoProjects_ReturnsEmpty()
    {
        IProjectLoader projectLoader = GetSubstitute<IProjectLoader>();
        IPackageRegistry packageRegistry = GetSubstitute<IPackageRegistry>();
        IPackageCache packageCache = GetSubstitute<IPackageCache>();

        PackageUpdater updater = this.CreateUpdater(
            projectLoader: projectLoader,
            packageRegistry: packageRegistry,
            packageCache: packageCache
        );
        PackageUpdateConfiguration config = MakeConfig("Test.Package");

        IReadOnlyList<PackageVersion> result = await updater.UpdateAsync(
            basePath: this.TempFolder,
            configuration: config,
            packageSources: NO_PACKAGE_SOURCES,
            cancellationToken: this.CancellationToken()
        );

        Assert.Empty(result);
        await projectLoader.DidNotReceive().LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_WithProjectsButNoMatchingPackages_ReturnsEmpty()
    {
        await this.CreateDummyCsprojAsync();

        IProject mockProject = GetSubstitute<IProject>();
        mockProject.Packages.Returns(
            [new PackageVersion(packageId: "Other.Package", version: new NuGetVersion("1.0.0"))]
        );

        IPackageRegistry packageRegistry = GetSubstitute<IPackageRegistry>();
        IPackageCache packageCache = GetSubstitute<IPackageCache>();

        PackageUpdater updater = this.CreateUpdater(
            projectLoader: new StaticProjectLoader(mockProject),
            packageRegistry: packageRegistry,
            packageCache: packageCache
        );
        PackageUpdateConfiguration config = MakeConfig("Test.Package");

        IReadOnlyList<PackageVersion> result = await updater.UpdateAsync(
            basePath: this.TempFolder,
            configuration: config,
            packageSources: NO_PACKAGE_SOURCES,
            cancellationToken: this.CancellationToken()
        );

        Assert.Empty(result);
        packageCache.DidNotReceive().GetVersions(Arg.Any<IReadOnlyList<string>>());
    }

    [Fact]
    public async Task UpdateAsync_WithMatchingPackages_WhenCacheHasAllVersions_ReturnsEmpty_WhenAlreadyUpToDate()
    {
        await this.CreateDummyCsprojAsync();

        PackageVersion installedVersion = new(packageId: "test.package", version: new NuGetVersion("1.0.0"));

        IProject mockProject = GetSubstitute<IProject>();
        mockProject.Packages.Returns(
            [new PackageVersion(packageId: "test.package", version: new NuGetVersion("1.0.0"))]
        );

        IPackageCache packageCache = GetSubstitute<IPackageCache>();
        packageCache.GetVersions(Arg.Any<IReadOnlyList<string>>()).Returns([installedVersion]);

        IPackageRegistry packageRegistry = GetSubstitute<IPackageRegistry>();

        PackageUpdater updater = this.CreateUpdater(
            projectLoader: new StaticProjectLoader(mockProject),
            packageRegistry: packageRegistry,
            packageCache: packageCache
        );
        PackageUpdateConfiguration config = MakeConfig("test.package");

        IReadOnlyList<PackageVersion> result = await updater.UpdateAsync(
            basePath: this.TempFolder,
            configuration: config,
            packageSources: NO_PACKAGE_SOURCES,
            cancellationToken: this.CancellationToken()
        );

        Assert.Empty(result);
    }

    [Fact]
    public async Task UpdateAsync_WithMatchingPackages_WhenRegistryFindsPackage_UpdatesProject()
    {
        await this.CreateDummyCsprojAsync();

        PackageVersion newVersion = new(packageId: "test.package", version: new NuGetVersion("2.0.0"));

        IProject mockProject = GetSubstitute<IProject>();
        mockProject.Packages.Returns(
            [new PackageVersion(packageId: "test.package", version: new NuGetVersion("1.0.0"))]
        );
        mockProject.UpdatePackage(Arg.Any<PackageVersion>()).Returns(true);
        mockProject.Changed.Returns(true);

        IPackageCache packageCache = GetSubstitute<IPackageCache>();
        packageCache.GetVersions(Arg.Any<IReadOnlyList<string>>()).Returns([]);

        PackageUpdater updater = this.CreateUpdater(
            projectLoader: new StaticProjectLoader(mockProject),
            packageRegistry: new StaticPackageRegistry([newVersion]),
            packageCache: packageCache
        );
        PackageUpdateConfiguration config = MakeConfig("test.package");

        IReadOnlyList<PackageVersion> result = await updater.UpdateAsync(
            basePath: this.TempFolder,
            configuration: config,
            packageSources: NO_PACKAGE_SOURCES,
            cancellationToken: this.CancellationToken()
        );

        Assert.Single(result);
        Assert.Equal(expected: new NuGetVersion("2.0.0"), actual: result[0].Version);
        Assert.True(
            StringComparer.OrdinalIgnoreCase.Equals(x: "test.package", y: result[0].PackageId),
            userMessage: "Package ID should match"
        );
        packageCache.Received(1).SetVersions(Arg.Any<IReadOnlyList<PackageVersion>>());
    }

    [Fact]
    public async Task UpdateAsync_WithMatchingPackages_WhenCacheHasPartialVersions_OnlyQueriesRegistryForMissingPackages()
    {
        await this.CreateDummyCsprojAsync();

        PackageVersion cachedInstalledVersion = new(packageId: "test.cached", version: new NuGetVersion("1.0.0"));
        PackageVersion cachedVersion = new(packageId: "test.cached", version: new NuGetVersion("1.0.0"));
        PackageVersion missingInstalledVersion = new(packageId: "test.missing", version: new NuGetVersion("1.0.0"));
        PackageVersion fetchedVersion = new(packageId: "test.missing", version: new NuGetVersion("2.0.0"));

        IProject mockProject = GetSubstitute<IProject>();
        mockProject.Packages.Returns([cachedInstalledVersion, missingInstalledVersion]);
        mockProject.UpdatePackage(Arg.Any<PackageVersion>()).Returns(true);
        mockProject.Changed.Returns(true);

        IPackageCache packageCache = GetSubstitute<IPackageCache>();
        packageCache.GetVersions(Arg.Any<IReadOnlyList<string>>()).Returns([cachedVersion]);

        IPackageRegistry packageRegistry = GetSubstitute<IPackageRegistry>();
        packageRegistry
            .FindPackagesAsync(
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns([fetchedVersion]);

        PackageUpdater updater = this.CreateUpdater(
            projectLoader: new StaticProjectLoader(mockProject),
            packageRegistry: packageRegistry,
            packageCache: packageCache
        );
        PackageUpdateConfiguration config = new(
            PackageMatch: new PackageMatch(PackageId: "test", Prefix: true),
            ExcludedPackages: []
        );

        IReadOnlyList<PackageVersion> result = await updater.UpdateAsync(
            basePath: this.TempFolder,
            configuration: config,
            packageSources: NO_PACKAGE_SOURCES,
            cancellationToken: this.CancellationToken()
        );

        Assert.Single(result);
        Assert.Equal(expected: new NuGetVersion("2.0.0"), actual: result[0].Version);
        Assert.True(
            StringComparer.OrdinalIgnoreCase.Equals(x: "test.missing", y: result[0].PackageId),
            userMessage: "Package ID should match"
        );

        await packageRegistry
            .Received(1)
            .FindPackagesAsync(
                Arg.Is<IReadOnlyList<string>>(ids =>
                    ids.Count == 1 && StringComparer.OrdinalIgnoreCase.Equals(x: "test.missing", y: ids[0])
                ),
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<CancellationToken>()
            );
        packageCache.Received(1).SetVersions(Arg.Any<IReadOnlyList<PackageVersion>>());
    }

    [Fact]
    public async Task UpdateAsync_WhenRegistryFindsNoPackages_ReturnsEmpty()
    {
        await this.CreateDummyCsprojAsync();

        IProject mockProject = GetSubstitute<IProject>();
        mockProject.Packages.Returns(
            [new PackageVersion(packageId: "test.package", version: new NuGetVersion("1.0.0"))]
        );

        IPackageCache packageCache = GetSubstitute<IPackageCache>();
        packageCache.GetVersions(Arg.Any<IReadOnlyList<string>>()).Returns([]);

        PackageUpdater updater = this.CreateUpdater(
            projectLoader: new StaticProjectLoader(mockProject),
            packageRegistry: new StaticPackageRegistry([]),
            packageCache: packageCache
        );
        PackageUpdateConfiguration config = MakeConfig("test.package");

        IReadOnlyList<PackageVersion> result = await updater.UpdateAsync(
            basePath: this.TempFolder,
            configuration: config,
            packageSources: NO_PACKAGE_SOURCES,
            cancellationToken: this.CancellationToken()
        );

        Assert.Empty(result);
    }

    private sealed class StaticProjectLoader : IProjectLoader
    {
        private readonly IProject? _project;

        public StaticProjectLoader(IProject? project)
        {
            this._project = project;
        }

        public ValueTask<IProject?> LoadAsync(string path, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(this._project);
        }

        public void Reset() { }
    }

    private sealed class StaticPackageRegistry : IPackageRegistry
    {
        private readonly IReadOnlyList<PackageVersion> _packages;

        public StaticPackageRegistry(IReadOnlyList<PackageVersion> packages)
        {
            this._packages = packages;
        }

        public ValueTask<IReadOnlyList<PackageVersion>> FindPackagesAsync(
            IReadOnlyList<string> packageIds,
            IReadOnlyList<string> packageSources,
            CancellationToken cancellationToken
        )
        {
            return ValueTask.FromResult(this._packages);
        }
    }
}
