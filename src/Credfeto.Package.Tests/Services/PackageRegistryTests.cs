using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Package.Exceptions;
using Credfeto.Package.Services;
using FunFair.Test.Common;
using Microsoft.Extensions.Logging;
using NonBlocking;
using NSubstitute;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Xunit;

namespace Credfeto.Package.Tests.Services;

public sealed class PackageRegistryTests : LoggingTestBase
{
    private const string DeadSourceUrl = "https://dead.example/index.json";
    private const string AliveSourceUrl = "https://alive.example/index.json";
    private const string DeadSourceUrl2 = "https://dead-two.example/index.json";
    private const string TestPackageId = "Test.Package";

    public PackageRegistryTests(ITestOutputHelper output)
        : base(output) { }

    private PackageRegistry CreateRegistry(IPackageMetadataFetcher metadataFetcher)
    {
        ILogger<PackageRegistry> logger = this.GetTypedLogger<PackageRegistry>();

        return new(metadataFetcher: metadataFetcher, logger: logger);
    }

    private static PackageSource CreateTestPackageSource()
    {
        return new(source: AliveSourceUrl, name: "Alive", isEnabled: true, isOfficial: true, isPersistable: true);
    }

    private static IEnumerable<IPackageSearchMetadata> MetadataFor(string packageId, string version)
    {
        IPackageSearchMetadata metadata = GetSubstitute<IPackageSearchMetadata>();
        metadata.Identity.Returns(new PackageIdentity(id: packageId, version: NuGetVersion.Parse(version)));

        return [metadata];
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
                    StringComparer.Ordinal.Equals(sourceRepository.PackageSource.Source, DeadSourceUrl)
                ),
                packageId: "Test.Package",
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Task.FromException<IEnumerable<IPackageSearchMetadata>>(new HttpRequestException("feed unreachable"))
            );

        metadataFetcher
            .GetMetadataAsync(
                Arg.Is<SourceRepository>(sourceRepository =>
                    StringComparer.Ordinal.Equals(sourceRepository.PackageSource.Source, AliveSourceUrl)
                ),
                packageId: "Test.Package",
                Arg.Any<CancellationToken>()
            )
            .Returns(_ => Task.FromResult(MetadataFor(packageId: "Test.Package", version: "1.2.3")));

        PackageRegistry registry = this.CreateRegistry(metadataFetcher);

        UpdateFailedException exception = await Assert.ThrowsAsync<UpdateFailedException>(() =>
            registry
                .FindPackagesAsync(
                    packageIds: ["Test.Package"],
                    packageSources: [DeadSourceUrl, AliveSourceUrl],
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
                    packageSources: [DeadSourceUrl, DeadSourceUrl2],
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
            packageSources: [AliveSourceUrl],
            cancellationToken: this.CancellationToken()
        );

        PackageVersion found = Assert.Single(result);
        Assert.Equal(expected: "Test.Package", actual: found.PackageId);
        Assert.Equal(expected: NuGetVersion.Parse("1.2.3"), actual: found.Version);
    }

    [Fact]
    public void RegisterFoundPackageVersion_WhenKeyNotPresent_AddsCandidate()
    {
        ConcurrentDictionary<string, NuGetVersion> found = new(StringComparer.Ordinal);
        PackageRegistry registry = this.CreateRegistry(GetSubstitute<IPackageMetadataFetcher>());

        registry.RegisterFoundPackageVersion(
            packageSource: CreateTestPackageSource(),
            found: found,
            packageId: TestPackageId,
            candidateVersion: NuGetVersion.Parse("1.2.3")
        );

        Assert.Equal(expected: NuGetVersion.Parse("1.2.3"), actual: found[TestPackageId]);
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
        ConcurrentDictionary<string, NuGetVersion> found = new(StringComparer.Ordinal);
        found[TestPackageId] = NuGetVersion.Parse(existing);
        PackageRegistry registry = this.CreateRegistry(GetSubstitute<IPackageMetadataFetcher>());

        registry.RegisterFoundPackageVersion(
            packageSource: CreateTestPackageSource(),
            found: found,
            packageId: TestPackageId,
            candidateVersion: NuGetVersion.Parse(candidate)
        );

        Assert.Equal(expected: NuGetVersion.Parse(expected), actual: found[TestPackageId]);
    }

    [Fact]
    public async Task RegisterFoundPackageVersion_WhenCalledConcurrently_NeverDropsTheHighestVersion()
    {
        PackageRegistry registry = this.CreateRegistry(GetSubstitute<IPackageMetadataFetcher>());
        PackageSource source = CreateTestPackageSource();
        NuGetVersion lowestVersion = NuGetVersion.Parse("1.0.0");
        NuGetVersion middleVersion = NuGetVersion.Parse("1.5.0");
        NuGetVersion highestVersion = NuGetVersion.Parse("2.0.0");

        CancellationToken cancellationToken = this.CancellationToken();

        for (int iteration = 0; iteration < 200; ++iteration)
        {
            ConcurrentDictionary<string, NuGetVersion> found = new(StringComparer.Ordinal);
            found[TestPackageId] = lowestVersion;

            await Task.WhenAll(
                Task.Run(
                    () =>
                        registry.RegisterFoundPackageVersion(
                            packageSource: source,
                            found: found,
                            packageId: TestPackageId,
                            candidateVersion: highestVersion
                        ),
                    cancellationToken
                ),
                Task.Run(
                    () =>
                        registry.RegisterFoundPackageVersion(
                            packageSource: source,
                            found: found,
                            packageId: TestPackageId,
                            candidateVersion: middleVersion
                        ),
                    cancellationToken
                )
            );

            Assert.Equal(expected: highestVersion, actual: found[TestPackageId]);
        }
    }
}
