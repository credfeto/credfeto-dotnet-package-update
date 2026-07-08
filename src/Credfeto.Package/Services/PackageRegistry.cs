using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Package.Services.LoggingExtensions;
using Microsoft.Extensions.Logging;
using NonBlocking;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Credfeto.Package.Services;

public sealed class PackageRegistry : IPackageRegistry
{
    private const bool INCLUDE_UNLISTED_PACKAGES = false;

    private readonly ILogger<PackageRegistry> _logger;

    public PackageRegistry(ILogger<PackageRegistry> logger)
    {
        this._logger = logger;
    }

    public async ValueTask<IReadOnlyList<PackageVersion>> FindPackagesAsync(
        IReadOnlyList<string> packageIds,
        IReadOnlyList<string> packageSources,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<PackageSource> sources = DefinePackageSources(packageSources);

        ConcurrentDictionary<string, NuGetVersion> packages = new(StringComparer.OrdinalIgnoreCase);

        foreach (string packageId in packageIds)
        {
            await this.FindPackageInSourcesAsync(
                sources: sources,
                packageId: packageId,
                packages: packages,
                cancellationToken: cancellationToken
            );
        }

        return [.. packages.Select(p => new PackageVersion(packageId: p.Key, version: p.Value))];
    }

    private static IReadOnlyList<PackageSource> DefinePackageSources(IReadOnlyList<string> sources)
    {
        PackageSourceProvider packageSourceProvider = new(Settings.LoadDefaultSettings(Environment.CurrentDirectory));

        return [.. packageSourceProvider.LoadPackageSources().Concat(sources.Select(CreateCustomPackageSource))];
    }

    private static PackageSource CreateCustomPackageSource(string source, int sourceId)
    {
        return new(source: source, $"Custom{sourceId}", isEnabled: true, isOfficial: true, isPersistable: true);
    }

    private async Task LoadPackagesFromSourceAsync(
        PackageSource packageSource,
        string packageId,
        ConcurrentDictionary<string, NuGetVersion> found,
        CancellationToken cancellationToken
    )
    {
        SourceRepository sourceRepository = new(source: packageSource, [.. Repository.Provider.GetCoreV3()]);

        PackageMetadataResource metadataResource = await sourceRepository.GetResourceAsync<PackageMetadataResource>(
            cancellationToken
        );

        using SourceCacheContext sourceCacheContext = new();

        IEnumerable<IPackageSearchMetadata> result = await metadataResource.GetMetadataAsync(
            packageId: packageId,
            includePrerelease: false,
            includeUnlisted: INCLUDE_UNLISTED_PACKAGES,
            sourceCacheContext: sourceCacheContext,
            log: NullLogger.Instance,
            token: cancellationToken
        );

        foreach (
            PackageVersion packageVersion in result
                .Select(entry => entry.Identity)
                .Select(identity => new PackageVersion(packageId: identity.Id, version: identity.Version))
                .Where(p => !p.Version.IsPrerelease && !IsBannedPackage(p))
        )
        {
            if (found.TryGetValue(key: packageVersion.PackageId, out NuGetVersion? existingVersion))
            {
                this.DoUpdateRegisteredFoundPackage(
                    packageSource: packageSource,
                    found: found,
                    existingVersion: existingVersion,
                    packageVersion: packageVersion
                );
            }
            else if (found.TryAdd(key: packageVersion.PackageId, value: packageVersion.Version))
            {
                this._logger.FoundPackageInSource(
                    packageId: packageVersion.PackageId,
                    version: packageVersion.Version,
                    source: packageSource.Source
                );
            }
        }
    }

    private void DoUpdateRegisteredFoundPackage(
        PackageSource packageSource,
        ConcurrentDictionary<string, NuGetVersion> found,
        NuGetVersion existingVersion,
        PackageVersion packageVersion
    )
    {
        // pick the latest feed always
        if (
            existingVersion < packageVersion.Version
            && found.TryUpdate(
                key: packageVersion.PackageId,
                newValue: packageVersion.Version,
                comparisonValue: existingVersion
            )
        )
        {
            this._logger.FoundPackageInSource(
                packageId: packageVersion.PackageId,
                version: packageVersion.Version,
                source: packageSource.Source
            );
        }
    }

    private static bool IsBannedPackage(PackageVersion packageVersion)
    {
        return packageVersion.Version.ToString().Contains(value: '+', comparisonType: StringComparison.Ordinal);
    }

    private async ValueTask FindPackageInSourcesAsync(
        IReadOnlyList<PackageSource> sources,
        string packageId,
        ConcurrentDictionary<string, NuGetVersion> packages,
        CancellationToken cancellationToken
    )
    {
        this._logger.EnumeratingPackageVersions(packageId);

        ConcurrentDictionary<string, NuGetVersion> found = new(StringComparer.Ordinal);

        await Task.WhenAll(
            sources.Select(selector: source =>
                this.LoadPackagesFromSourceAsync(
                    packageSource: source,
                    packageId: packageId,
                    found: found,
                    cancellationToken: cancellationToken
                )
            )
        );

        foreach ((string key, NuGetVersion value) in found)
        {
            packages.TryAdd(key: key, value: value);
        }
    }
}
