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

    [Fact]
    public async Task FindPackagesAsync_WhenOneSourceFails_ReturnsResultsFromRemainingSources()
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

        IReadOnlyList<PackageVersion> result = await registry.FindPackagesAsync(
            packageIds: ["Test.Package"],
            packageSources: [DeadSourceUrl, AliveSourceUrl],
            cancellationToken: this.CancellationToken()
        );

        PackageVersion found = Assert.Single(result);
        Assert.Equal(expected: "Test.Package", actual: found.PackageId);
        Assert.Equal(expected: NuGetVersion.Parse("1.2.3"), actual: found.Version);
    }

    [Fact]
    public Task FindPackagesAsync_WhenAllSourcesFail_ThrowsUpdateFailedException()
    {
        IPackageMetadataFetcher metadataFetcher = GetSubstitute<IPackageMetadataFetcher>();

        metadataFetcher
            .GetMetadataAsync(Arg.Any<PackageSource>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromException<IEnumerable<IPackageSearchMetadata>>(new HttpRequestException("feed unreachable"))
            );

        PackageRegistry registry = this.CreateRegistry(metadataFetcher);

        return Assert.ThrowsAsync<UpdateFailedException>(() =>
            registry
                .FindPackagesAsync(
                    packageIds: ["Test.Package"],
                    packageSources: [DeadSourceUrl, DeadSourceUrl2],
                    cancellationToken: this.CancellationToken()
                )
                .AsTask()
        );
    }
}
