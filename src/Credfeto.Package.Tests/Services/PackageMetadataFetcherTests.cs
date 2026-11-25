using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Package.Exceptions;
using Credfeto.Package.Services;
using FunFair.Test.Common;
using NSubstitute;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using Xunit;

namespace Credfeto.Package.Tests.Services;

public sealed class PackageMetadataFetcherTests : TestBase
{
    private const string SOURCE_URL = "https://no-providers.example/index.json";
    private const string PACKAGE_ID = "Test.Package";

    [Fact]
    public async Task GetMetadataThrowsWhenSourceHasNoMetadataResourceAsync()
    {
        SourceRepository sourceRepository = CreateSourceRepository([]);
        PackageMetadataFetcher fetcher = new();

        UpdateFailedException exception = await Assert.ThrowsAsync<UpdateFailedException>(() =>
            fetcher.GetMetadataAsync(
                sourceRepository: sourceRepository,
                packageId: PACKAGE_ID,
                cancellationToken: this.CancellationToken()
            )
        );

        Assert.Contains(
            expectedSubstring: SOURCE_URL,
            actualString: exception.Message,
            comparisonType: StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task GetMetadataReturnsListedReleaseMetadataFromSourceAsync()
    {
        IReadOnlyList<IPackageSearchMetadata> expected = [GetSubstitute<IPackageSearchMetadata>()];

        PackageMetadataResource metadataResource = GetSubstitute<PackageMetadataResource>();
        metadataResource
            .GetMetadataAsync(
                packageId: PACKAGE_ID,
                includePrerelease: false,
                includeUnlisted: false,
                sourceCacheContext: Arg.Any<SourceCacheContext>(),
                log: Arg.Any<ILogger>(),
                token: Arg.Any<CancellationToken>()
            )
            .Returns(expected);

        SourceRepository sourceRepository = CreateSourceRepository([CreateProvider(metadataResource)]);
        PackageMetadataFetcher fetcher = new();

        IEnumerable<IPackageSearchMetadata> actual = await fetcher.GetMetadataAsync(
            sourceRepository: sourceRepository,
            packageId: PACKAGE_ID,
            cancellationToken: this.CancellationToken()
        );

        Assert.Same(expected: expected, actual: actual);
    }

    private static SourceRepository CreateSourceRepository(IEnumerable<INuGetResourceProvider> providers)
    {
        PackageSource packageSource = new(
            source: SOURCE_URL,
            name: "TestSource",
            isEnabled: true,
            isOfficial: true,
            isPersistable: true
        );

        return new(source: packageSource, providers: providers);
    }

    private static INuGetResourceProvider CreateProvider(INuGetResource resource)
    {
        INuGetResourceProvider provider = GetSubstitute<INuGetResourceProvider>();
        provider.ResourceType.Returns(typeof(PackageMetadataResource));
        provider.Name.Returns(nameof(PackageMetadataResource));
        provider.Before.Returns([]);
        provider.After.Returns([]);
        provider
            .TryCreate(source: Arg.Any<SourceRepository>(), token: Arg.Any<CancellationToken>())
            .Returns(Tuple.Create<bool, INuGetResource?>(item1: true, item2: resource));

        return provider;
    }
}
