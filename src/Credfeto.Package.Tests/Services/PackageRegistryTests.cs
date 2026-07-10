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
    private const string DeadSourceUrl = "https://dead.example/index.json";
    private const string AliveSourceUrl = "https://alive.example/index.json";
    private const string DeadSourceUrl2 = "https://dead-two.example/index.json";

    public PackageRegistryTests(ITestOutputHelper output)
        : base(output) { }

    private PackageRegistry CreateRegistry(IPackageMetadataFetcher metadataFetcher)
    {
        ILogger<PackageRegistry> logger = this.GetTypedLogger<PackageRegistry>();

        return new(metadataFetcher: metadataFetcher, logger: logger);
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
                Arg.Is<PackageSource>(source => StringComparer.Ordinal.Equals(source.Source, DeadSourceUrl)),
                packageId: "Test.Package",
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Task.FromException<IEnumerable<IPackageSearchMetadata>>(new HttpRequestException("feed unreachable"))
            );

        metadataFetcher
            .GetMetadataAsync(
                Arg.Is<PackageSource>(source => StringComparer.Ordinal.Equals(source.Source, AliveSourceUrl)),
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
            .GetMetadataAsync(Arg.Any<PackageSource>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
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
            .GetMetadataAsync(Arg.Any<PackageSource>(), packageId: "Test.Package", Arg.Any<CancellationToken>())
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
}
