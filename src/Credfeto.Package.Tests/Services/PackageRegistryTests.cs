using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Package.Exceptions;
using Credfeto.Package.Services;
using FunFair.Test.Common;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Xunit;

namespace Credfeto.Package.Tests.Services;

public sealed class PackageRegistryTests : LoggingTestBase
{
    private const string DEAD_SOURCE_URL = "https://dead.example/index.json";
    private const string ALIVE_SOURCE_URL = "https://alive.example/index.json";
    private const string DEAD_SOURCE_URL_2 = "https://dead-two.example/index.json";
    private const string TEST_PACKAGE_ID = "Test.Package";

    public PackageRegistryTests(ITestOutputHelper output)
        : base(output) { }

    private PackageRegistry CreateRegistry(IPackageMetadataFetcher metadataFetcher)
    {
        ILogger<PackageRegistry> logger = this.GetTypedLogger<PackageRegistry>();

        return new(metadataFetcher: metadataFetcher, logger: logger);
    }

    private static PackageSource CreateTestPackageSource()
    {
        return new(source: ALIVE_SOURCE_URL, name: "Alive", isEnabled: true, isOfficial: true, isPersistable: true);
    }

    private static IEnumerable<IPackageSearchMetadata> MetadataFor(string packageId, string version)
    {
        IPackageSearchMetadata metadata = GetSubstitute<IPackageSearchMetadata>();
        metadata.Identity.Returns(new PackageIdentity(id: packageId, version: NuGetVersion.Parse(version)));

        return [metadata];
    }

    private static void MockPackageMetadataFetcherGetMetadata(
        IPackageMetadataFetcher metadataFetcher,
        string sourceUrl,
        string requestedPackageId,
        string returnedPackageId,
        string version
    )
    {
        metadataFetcher
            .GetMetadataAsync(
                Arg.Is<SourceRepository>(sourceRepository =>
                    StringComparer.Ordinal.Equals(sourceRepository.PackageSource.Source, sourceUrl)
                ),
                packageId: requestedPackageId,
                Arg.Any<CancellationToken>()
            )
            .Returns(_ => Task.FromResult(MetadataFor(packageId: returnedPackageId, version: version)));
    }

    private static (string FailedPart, string SucceededPart) SplitOnSucceeded(string message)
    {
        const string marker = "Succeeded: ";
        int index = message.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(index >= 0, $"Expected message to contain '{marker}': {message}");

        return (message[..index], message[index..]);
    }

    [Fact]
    public async Task FindPackagesAsync_WhenOneSourceFails_ThrowsUpdateFailedException()
    {
        IPackageMetadataFetcher metadataFetcher = GetSubstitute<IPackageMetadataFetcher>();

        metadataFetcher
            .GetMetadataAsync(
                Arg.Is<SourceRepository>(sourceRepository =>
                    StringComparer.Ordinal.Equals(sourceRepository.PackageSource.Source, DEAD_SOURCE_URL)
                ),
                packageId: "Test.Package",
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Task.FromException<IEnumerable<IPackageSearchMetadata>>(new HttpRequestException("feed unreachable"))
            );

        MockPackageMetadataFetcherGetMetadata(
            metadataFetcher,
            sourceUrl: ALIVE_SOURCE_URL,
            requestedPackageId: "Test.Package",
            returnedPackageId: "Test.Package",
            version: "1.2.3"
        );

        PackageRegistry registry = this.CreateRegistry(metadataFetcher);

        UpdateFailedException exception = await Assert.ThrowsAsync<UpdateFailedException>(() =>
            registry
                .FindPackagesAsync(
                    packageIds: ["Test.Package"],
                    packageSources: [DEAD_SOURCE_URL, ALIVE_SOURCE_URL],
                    cancellationToken: this.CancellationToken()
                )
                .AsTask()
        );

        (string failedPart, string succeededPart) = SplitOnSucceeded(exception.Message);

        Assert.Contains(
            expectedSubstring: "Custom0",
            actualString: failedPart,
            comparisonType: StringComparison.Ordinal
        );
        Assert.DoesNotContain(
            expectedSubstring: "Custom1",
            actualString: failedPart,
            comparisonType: StringComparison.Ordinal
        );
        Assert.Contains(
            expectedSubstring: "Custom1",
            actualString: succeededPart,
            comparisonType: StringComparison.Ordinal
        );
        Assert.DoesNotContain(
            expectedSubstring: "Custom0",
            actualString: succeededPart,
            comparisonType: StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task FindPackagesAsync_WhenAllSourcesFail_ThrowsUpdateFailedException()
    {
        IPackageMetadataFetcher metadataFetcher = GetSubstitute<IPackageMetadataFetcher>();

        metadataFetcher
            .GetMetadataAsync(Arg.Any<SourceRepository>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromException<IEnumerable<IPackageSearchMetadata>>(new HttpRequestException("feed unreachable"))
            );

        PackageRegistry registry = this.CreateRegistry(metadataFetcher);

        UpdateFailedException exception = await Assert.ThrowsAsync<UpdateFailedException>(() =>
            registry
                .FindPackagesAsync(
                    packageIds: ["Test.Package"],
                    packageSources: [DEAD_SOURCE_URL, DEAD_SOURCE_URL_2],
                    cancellationToken: this.CancellationToken()
                )
                .AsTask()
        );

        Assert.Contains(
            expectedSubstring: "Custom0",
            actualString: exception.Message,
            comparisonType: StringComparison.Ordinal
        );
        Assert.Contains(
            expectedSubstring: "Custom1",
            actualString: exception.Message,
            comparisonType: StringComparison.Ordinal
        );
        Assert.Contains(
            expectedSubstring: "Succeeded: (none)",
            actualString: exception.Message,
            comparisonType: StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task FindPackagesAsync_WhenAllSourcesSucceed_ReturnsResults()
    {
        IPackageMetadataFetcher metadataFetcher = GetSubstitute<IPackageMetadataFetcher>();

        metadataFetcher
            .GetMetadataAsync(Arg.Any<SourceRepository>(), packageId: "Test.Package", Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(MetadataFor(packageId: "Test.Package", version: "1.2.3")));

        PackageRegistry registry = this.CreateRegistry(metadataFetcher);

        IReadOnlyList<PackageVersion> result = await registry.FindPackagesAsync(
            packageIds: ["Test.Package"],
            packageSources: [ALIVE_SOURCE_URL],
            cancellationToken: this.CancellationToken()
        );

        PackageVersion found = Assert.Single(result);
        Assert.Equal(expected: "Test.Package", actual: found.PackageId);
        Assert.Equal(expected: NuGetVersion.Parse("1.2.3"), actual: found.Version);
    }

    [Fact]
    public async Task FindPackagesAsync_WhenMultipleSourcesReturnDifferentVersions_ReturnsTheHighestVersion()
    {
        IPackageMetadataFetcher metadataFetcher = GetSubstitute<IPackageMetadataFetcher>();

        MockPackageMetadataFetcherGetMetadata(
            metadataFetcher,
            sourceUrl: ALIVE_SOURCE_URL,
            requestedPackageId: "Test.Package",
            returnedPackageId: "Test.Package",
            version: "1.5.0"
        );
        MockPackageMetadataFetcherGetMetadata(
            metadataFetcher,
            sourceUrl: DEAD_SOURCE_URL_2,
            requestedPackageId: "Test.Package",
            returnedPackageId: "Test.Package",
            version: "2.0.0"
        );

        PackageRegistry registry = this.CreateRegistry(metadataFetcher);

        IReadOnlyList<PackageVersion> result = await registry.FindPackagesAsync(
            packageIds: ["Test.Package"],
            packageSources: [ALIVE_SOURCE_URL, DEAD_SOURCE_URL_2],
            cancellationToken: this.CancellationToken()
        );

        PackageVersion found = Assert.Single(result);
        Assert.Equal(expected: "Test.Package", actual: found.PackageId);
        Assert.Equal(expected: NuGetVersion.Parse("2.0.0"), actual: found.Version);
    }

    [Fact]
    public async Task FindPackagesAsync_WhenSourcesDisagreeOnPackageIdCasing_ReturnsTheHighestVersion()
    {
        IPackageMetadataFetcher metadataFetcher = GetSubstitute<IPackageMetadataFetcher>();

        MockPackageMetadataFetcherGetMetadata(
            metadataFetcher,
            sourceUrl: ALIVE_SOURCE_URL,
            requestedPackageId: "Foo.Bar",
            returnedPackageId: "Foo.Bar",
            version: "1.0.0"
        );
        MockPackageMetadataFetcherGetMetadata(
            metadataFetcher,
            sourceUrl: DEAD_SOURCE_URL_2,
            requestedPackageId: "Foo.Bar",
            returnedPackageId: "foo.bar",
            version: "2.0.0"
        );

        PackageRegistry registry = this.CreateRegistry(metadataFetcher);

        IReadOnlyList<PackageVersion> result = await registry.FindPackagesAsync(
            packageIds: ["Foo.Bar"],
            packageSources: [ALIVE_SOURCE_URL, DEAD_SOURCE_URL_2],
            cancellationToken: this.CancellationToken()
        );

        PackageVersion found = Assert.Single(result);
        Assert.Equal(expected: "foo.bar", actual: found.PackageId);
        Assert.Equal(expected: NuGetVersion.Parse("2.0.0"), actual: found.Version);
    }

    [Fact]
    public async Task FindPackagesAsync_WhenLaterSourceHasLowerVersionWithDifferentCasing_KeepsWinnerCasing()
    {
        IPackageMetadataFetcher metadataFetcher = GetSubstitute<IPackageMetadataFetcher>();

        MockPackageMetadataFetcherGetMetadata(
            metadataFetcher,
            sourceUrl: ALIVE_SOURCE_URL,
            requestedPackageId: "Foo.Bar",
            returnedPackageId: "Foo.Bar",
            version: "2.0.0"
        );
        MockPackageMetadataFetcherGetMetadata(
            metadataFetcher,
            sourceUrl: DEAD_SOURCE_URL_2,
            requestedPackageId: "Foo.Bar",
            returnedPackageId: "foo.bar",
            version: "1.0.0"
        );

        PackageRegistry registry = this.CreateRegistry(metadataFetcher);

        IReadOnlyList<PackageVersion> result = await registry.FindPackagesAsync(
            packageIds: ["Foo.Bar"],
            packageSources: [ALIVE_SOURCE_URL, DEAD_SOURCE_URL_2],
            cancellationToken: this.CancellationToken()
        );

        PackageVersion found = Assert.Single(result);
        Assert.Equal(expected: "Foo.Bar", actual: found.PackageId);
        Assert.Equal(expected: NuGetVersion.Parse("2.0.0"), actual: found.Version);
    }

    [Fact]
    public void RegisterFoundPackageVersion_WhenKeyNotPresent_AddsCandidate()
    {
        Dictionary<string, PackageVersion> found = new(StringComparer.Ordinal);
        PackageRegistry registry = this.CreateRegistry(GetSubstitute<IPackageMetadataFetcher>());

        registry.RegisterFoundPackageVersion(
            packageSource: CreateTestPackageSource(),
            found: found,
            candidate: new PackageVersion(packageId: TEST_PACKAGE_ID, NuGetVersion.Parse("1.2.3"))
        );

        Assert.Equal(expected: NuGetVersion.Parse("1.2.3"), actual: found[TEST_PACKAGE_ID].Version);
    }

    [Theory]
    [InlineData("1.0.0", "2.0.0", "2.0.0")] // higher candidate replaces the stored version
    [InlineData("2.0.0", "1.0.0", "2.0.0")] // lower candidate is discarded
    [InlineData("1.0.0", "1.0.0", "1.0.0")] // equal candidate is a no-op
    public void RegisterFoundPackageVersion_WhenKeyAlreadyPresent_KeepsTheHigherVersion(
        string existing,
        string candidate,
        string expected
    )
    {
        Dictionary<string, PackageVersion> found = new(StringComparer.Ordinal)
        {
            [TEST_PACKAGE_ID] = new(packageId: TEST_PACKAGE_ID, NuGetVersion.Parse(existing)),
        };
        PackageRegistry registry = this.CreateRegistry(GetSubstitute<IPackageMetadataFetcher>());

        registry.RegisterFoundPackageVersion(
            packageSource: CreateTestPackageSource(),
            found: found,
            candidate: new PackageVersion(packageId: TEST_PACKAGE_ID, NuGetVersion.Parse(candidate))
        );

        Assert.Equal(expected: NuGetVersion.Parse(expected), actual: found[TEST_PACKAGE_ID].Version);
    }
}
