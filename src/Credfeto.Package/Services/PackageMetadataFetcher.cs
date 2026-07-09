using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;

namespace Credfeto.Package.Services;

public sealed class PackageMetadataFetcher : IPackageMetadataFetcher
{
    private const bool INCLUDE_UNLISTED_PACKAGES = false;

    public async Task<IEnumerable<IPackageSearchMetadata>> GetMetadataAsync(
        PackageSource packageSource,
        string packageId,
        CancellationToken cancellationToken
    )
    {
        SourceRepository sourceRepository = new(source: packageSource, [.. Repository.Provider.GetCoreV3()]);

        PackageMetadataResource metadataResource = await sourceRepository.GetResourceAsync<PackageMetadataResource>(
            cancellationToken
        );

        using SourceCacheContext sourceCacheContext = new();

        return await metadataResource.GetMetadataAsync(
            packageId: packageId,
            includePrerelease: false,
            includeUnlisted: INCLUDE_UNLISTED_PACKAGES,
            sourceCacheContext: sourceCacheContext,
            log: NullLogger.Instance,
            token: cancellationToken
        );
    }
}
