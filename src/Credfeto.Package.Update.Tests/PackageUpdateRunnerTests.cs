using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Package.Update.Exceptions;
using FunFair.Test.Common;
using NSubstitute;
using NuGet.Versioning;
using Xunit;

namespace Credfeto.Package.Update.Tests;

public sealed class PackageUpdateRunnerTests : TestBase
{
    private const string FOLDER = "/src";

    private readonly IDiagnosticLogger _logger;
    private readonly IPackageCache _packageCache;
    private readonly IPackageUpdater _packageUpdater;
    private readonly PackageUpdateRunner _runner;

    public PackageUpdateRunnerTests()
    {
        this._packageCache = GetSubstitute<IPackageCache>();
        this._logger = GetSubstitute<IDiagnosticLogger>();
        this._packageUpdater = GetSubstitute<IPackageUpdater>();

        this._runner = new(
            packageCache: this._packageCache,
            logger: this._logger,
            packageUpdater: this._packageUpdater
        );
    }

    [Theory]
    [InlineData("Test.Package", " ")]
    [InlineData(" ", FOLDER)]
    public async Task UpdateAsyncWithBlankRequiredOptionThrowsInvalidOptionsAsync(string packageId, string folder)
    {
        await using StringWriter output = new();

        await Assert.ThrowsAsync<InvalidOptionsException>(() =>
            this.RunAsync(options: CreateOptions(packageId: packageId, folder: folder, cache: null), output: output)
        );
    }

    [Fact]
    public async Task UpdateAsyncWithUpdatesReturnsSuccessAndWritesGitHubEnvironmentAsync()
    {
        this.UpdaterReturns([new(packageId: "Test.Package", version: new NuGetVersion("1.2.3"))]);
        string gitHubEnvFilePath = Path.GetTempFileName();

        try
        {
            await using StringWriter output = new();

            int result = await this._runner.UpdateAsync(
                options: CreateOptions(packageId: "Test:Prefix", folder: FOLDER, cache: null) with
                {
                    Exclude = ["Test.Excluded", "Test.Other:prefix"],
                },
                gitHubEnvFilePath: gitHubEnvFilePath,
                output: output,
                cancellationToken: this.CancellationToken()
            );

            Assert.Equal(expected: PackageUpdateRunner.SUCCESS, actual: result);
            Assert.Equal(
                expected: ["Test.Package=1.2.3"],
                actual: await File.ReadAllLinesAsync(
                    path: gitHubEnvFilePath,
                    cancellationToken: this.CancellationToken()
                )
            );
            Assert.Equal(
                expected: "Including Test (Using Prefix match: True)"
                    + Environment.NewLine
                    + "Excluding Test.Excluded (Using Prefix match: False)"
                    + Environment.NewLine
                    + "Excluding Test.Other (Using Prefix match: True)"
                    + Environment.NewLine
                    + "Total updates: 1"
                    + Environment.NewLine,
                actual: output.ToString()
            );
            await this
                ._packageUpdater.Received(1)
                .UpdateAsync(
                    basePath: FOLDER,
                    configuration: Arg.Is<PackageUpdateConfiguration>(c =>
                        c.PackageMatch == new PackageMatch(PackageId: "Test", Prefix: true)
                        && c.ExcludedPackages.Count == 2
                    ),
                    packageSources: Arg.Any<IReadOnlyList<string>>(),
                    cancellationToken: Arg.Any<CancellationToken>()
                );
            await this._packageCache.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            File.Delete(gitHubEnvFilePath);
        }
    }

    [Fact]
    public async Task UpdateAsyncWithExistingCacheLoadsAndSavesCacheAsync()
    {
        this.UpdaterReturns([new(packageId: "Test.Package", version: new NuGetVersion("1.2.3"))]);
        string cacheFileName = Path.GetTempFileName();

        try
        {
            await using StringWriter output = new();

            int result = await this.RunAsync(
                options: CreateOptions(packageId: "Test.Package", folder: FOLDER, cache: cacheFileName),
                output: output
            );

            Assert.Equal(expected: PackageUpdateRunner.SUCCESS, actual: result);
            await this._packageCache.Received(1).LoadAsync(fileName: cacheFileName, Arg.Any<CancellationToken>());
            await this._packageCache.Received(1).SaveAsync(fileName: cacheFileName, Arg.Any<CancellationToken>());
        }
        finally
        {
            File.Delete(cacheFileName);
        }
    }

    [Fact]
    public async Task UpdateAsyncWithMissingCacheSavesWithoutLoadingAsync()
    {
        this.UpdaterReturns([new(packageId: "Test.Package", version: new NuGetVersion("1.2.3"))]);
        string cacheFileName = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        await using StringWriter output = new();

        int result = await this.RunAsync(
            options: CreateOptions(packageId: "Test.Package", folder: FOLDER, cache: cacheFileName),
            output: output
        );

        Assert.Equal(expected: PackageUpdateRunner.SUCCESS, actual: result);
        await this._packageCache.DidNotReceive().LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await this._packageCache.Received(1).SaveAsync(fileName: cacheFileName, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsyncWhenErrorsWereLoggedThrowsPackageUpdateExceptionAsync()
    {
        this.UpdaterReturns([new(packageId: "Test.Package", version: new NuGetVersion("1.2.3"))]);
        this._logger.IsErrored.Returns(true);
        await using StringWriter output = new();

        await Assert.ThrowsAsync<PackageUpdateException>(() =>
            this.RunAsync(
                options: CreateOptions(packageId: "Test.Package", folder: FOLDER, cache: null),
                output: output
            )
        );
    }

    [Fact]
    public async Task UpdateAsyncWithNoUpdatesThrowsNoPackagesUpdatedExceptionAsync()
    {
        this.UpdaterReturns([]);
        await using StringWriter output = new();

        await Assert.ThrowsAsync<NoPackagesUpdatedException>(() =>
            this.RunAsync(
                options: CreateOptions(packageId: "Test.Package", folder: FOLDER, cache: null),
                output: output
            )
        );
    }

    private static Options CreateOptions(string packageId, string folder, string? cache)
    {
        return new(PackageId: packageId, Folder: folder, Cache: cache, Source: [], Exclude: []);
    }

    private void UpdaterReturns(IReadOnlyList<PackageVersion> updates)
    {
        this._packageUpdater.UpdateAsync(
                basePath: Arg.Any<string>(),
                configuration: Arg.Any<PackageUpdateConfiguration>(),
                packageSources: Arg.Any<IReadOnlyList<string>>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(updates);
    }

    private Task<int> RunAsync(Options options, TextWriter output)
    {
        return this._runner.UpdateAsync(
            options: options,
            gitHubEnvFilePath: null,
            output: output,
            cancellationToken: this.CancellationToken()
        );
    }
}
