using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Package.Exceptions;
using Credfeto.Package.Services.LoggingExtensions;
using Microsoft.Extensions.Logging;
using NonBlocking;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Credfeto.Package.Services;

public sealed class PackageRegistry : IPackageRegistry
{
    private readonly IPackageMetadataFetcher _metadataFetcher;
    private readonly ILogger<PackageRegistry> _logger;

    public PackageRegistry(IPackageMetadataFetcher metadataFetcher, ILogger<PackageRegistry> logger)
    {
        this._metadataFetcher = metadataFetcher;
        this._logger = logger;
    }

    public async ValueTask<IReadOnlyList<PackageVersion>> FindPackagesAsync(
        IReadOnlyList<string> packageIds,
        IReadOnlyList<string> packageSources,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<SourceRepository> sourceRepositories = DefineSourceRepositories(packageSources);

        ConcurrentDictionary<string, NuGetVersion> packages = new(StringComparer.OrdinalIgnoreCase);

        foreach (string packageId in packageIds)
        {
            await this.FindPackageInSourcesAsync(
                sourceRepositories: sourceRepositories,
                packageId: packageId,
                packages: packages,
                cancellationToken: cancellationToken
            );
        }

        return [.. packages.Select(p => new PackageVersion(packageId: p.Key, version: p.Value))];
    }

    private static IReadOnlyList<SourceRepository> DefineSourceRepositories(IReadOnlyList<string> sources)
    {
        PackageSourceProvider packageSourceProvider = new(Settings.LoadDefaultSettings(Environment.CurrentDirectory));

        IReadOnlyList<PackageSource> packageSources =
        [
            .. packageSourceProvider.LoadPackageSources().Concat(sources.Select(CreateCustomPackageSource)),
        ];

        IReadOnlyList<Lazy<INuGetResourceProvider>> providers = [.. Repository.Provider.GetCoreV3()];

        return [.. packageSources.Select(packageSource => new SourceRepository(source: packageSource, providers))];
    }

    private static PackageSource CreateCustomPackageSource(string source, int sourceId)
    {
        return new(source: source, $"Custom{sourceId}", isEnabled: true, isOfficial: true, isPersistable: true);
    }

    private async Task LoadPackagesFromSourceAsync(
        SourceRepository sourceRepository,
        string packageId,
        ConcurrentDictionary<string, NuGetVersion> found,
        CancellationToken cancellationToken
    )
    {
        IEnumerable<IPackageSearchMetadata> result = await this._metadataFetcher.GetMetadataAsync(
            sourceRepository: sourceRepository,
            packageId: packageId,
            cancellationToken: cancellationToken
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
                    packageSource: sourceRepository.PackageSource,
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
                    source: sourceRepository.PackageSource.Source
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
        IReadOnlyList<SourceRepository> sourceRepositories,
        string packageId,
        ConcurrentDictionary<string, NuGetVersion> packages,
        CancellationToken cancellationToken
    )
    {
        this._logger.EnumeratingPackageVersions(packageId);

        ConcurrentDictionary<string, NuGetVersion> found = new(StringComparer.Ordinal);

        Exception?[] failures = await Task.WhenAll(
            sourceRepositories.Select(selector: sourceRepository =>
                this.LoadPackagesFromSourceSafeAsync(
                    sourceRepository: sourceRepository,
                    packageId: packageId,
                    found: found,
                    cancellationToken: cancellationToken
                )
            )
        );

        if (failures.Any(failure => failure is not null))
        {
            IReadOnlyList<(SourceRepository SourceRepository, Exception? Failure)> outcomes =
            [
                .. sourceRepositories.Zip(failures),
            ];
            IReadOnlyList<string> failedSources =
            [
                .. outcomes.Where(o => o.Failure is not null).Select(o => o.SourceRepository.PackageSource.Name),
            ];
            IReadOnlyList<string> succeededSources =
            [
                .. outcomes.Where(o => o.Failure is null).Select(o => o.SourceRepository.PackageSource.Name),
            ];
            string succeededSourcesMessage =
                succeededSources.Count == 0 ? "(none)" : string.Join(separator: ", ", succeededSources);

            this._logger.PackageSourcesFailed(
                failedCount: failedSources.Count,
                sourceCount: sourceRepositories.Count,
                packageId: packageId,
                failedSources: string.Join(separator: ", ", failedSources),
                succeededSources: succeededSourcesMessage
            );

            throw new UpdateFailedException(
                $"{failedSources.Count} of {sourceRepositories.Count} package source(s) failed while looking up {packageId}: {string.Join(separator: ", ", failedSources)}. Succeeded: {succeededSourcesMessage}",
                new AggregateException(failures.OfType<Exception>())
            );
        }

        foreach ((string key, NuGetVersion value) in found)
        {
            packages.TryAdd(key: key, value: value);
        }
    }

    private async Task<Exception?> LoadPackagesFromSourceSafeAsync(
        SourceRepository sourceRepository,
        string packageId,
        ConcurrentDictionary<string, NuGetVersion> found,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await this.LoadPackagesFromSourceAsync(
                sourceRepository: sourceRepository,
                packageId: packageId,
                found: found,
                cancellationToken: cancellationToken
            );

            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            this._logger.PackageSourceQueryFailed(
                source: sourceRepository.PackageSource.Name,
                packageId: packageId,
                message: exception.Message,
                exception: exception
            );

            return exception;
        }
    }
}
