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

    private async Task<IReadOnlyList<PackageVersion>> LoadPackagesFromSourceAsync(
        SourceRepository sourceRepository,
        string packageId,
        CancellationToken cancellationToken
    )
    {
        IEnumerable<IPackageSearchMetadata> result = await this._metadataFetcher.GetMetadataAsync(
            sourceRepository: sourceRepository,
            packageId: packageId,
            cancellationToken: cancellationToken
        );

        return
        [
            .. result
                .Select(entry => entry.Identity)
                .Select(identity => new PackageVersion(packageId: identity.Id, version: identity.Version))
                .Where(p => !p.Version.IsPrerelease && !IsBannedPackage(p)),
        ];
    }

    // merged single-threaded after the Task.WhenAll barrier in FindPackageInSourcesAsync, so the
    // highest candidate across sources is never dropped by a lost concurrent-write race (see #569).
    public void RegisterFoundPackageVersion(
        PackageSource packageSource,
        IDictionary<string, NuGetVersion> found,
        string packageId,
        NuGetVersion candidateVersion
    )
    {
        if (found.TryGetValue(key: packageId, out NuGetVersion? existingVersion) && existingVersion >= candidateVersion)
        {
            return;
        }

        found[packageId] = candidateVersion;

        this._logger.FoundPackageInSource(
            packageId: packageId,
            version: candidateVersion,
            source: packageSource.Source
        );
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

        (IReadOnlyList<PackageVersion> Found, Exception? Failure)[] outcomes = await Task.WhenAll(
            sourceRepositories.Select(selector: sourceRepository =>
                this.LoadPackagesFromSourceSafeAsync(
                    sourceRepository: sourceRepository,
                    packageId: packageId,
                    cancellationToken: cancellationToken
                )
            )
        );

        if (outcomes.Any(outcome => outcome.Failure is not null))
        {
            this.ThrowForFailedSources(sourceRepositories: sourceRepositories, packageId: packageId, outcomes: outcomes);
        }

        // sources ran concurrently above; the Task.WhenAll barrier means every source's results are
        // already collected by this point, so merging them here can run single-threaded (see #569).
        Dictionary<string, NuGetVersion> found = this.MergeFoundVersions(
            sourceRepositories: sourceRepositories,
            outcomes: outcomes
        );

        foreach ((string key, NuGetVersion value) in found)
        {
            packages.TryAdd(key: key, value: value);
        }
    }

    private Dictionary<string, NuGetVersion> MergeFoundVersions(
        IReadOnlyList<SourceRepository> sourceRepositories,
        (IReadOnlyList<PackageVersion> Found, Exception? Failure)[] outcomes
    )
    {
        Dictionary<string, NuGetVersion> found = new(StringComparer.Ordinal);

        for (int index = 0; index < sourceRepositories.Count; ++index)
        {
            PackageSource packageSource = sourceRepositories[index].PackageSource;

            foreach (PackageVersion packageVersion in outcomes[index].Found)
            {
                this.RegisterFoundPackageVersion(
                    packageSource: packageSource,
                    found: found,
                    packageId: packageVersion.PackageId,
                    candidateVersion: packageVersion.Version
                );
            }
        }

        return found;
    }

    private void ThrowForFailedSources(
        IReadOnlyList<SourceRepository> sourceRepositories,
        string packageId,
        (IReadOnlyList<PackageVersion> Found, Exception? Failure)[] outcomes
    )
    {
        IReadOnlyList<(string SourceName, Exception? Failure)> namedOutcomes =
        [
            .. sourceRepositories.Zip(
                outcomes,
                (sourceRepository, outcome) => (sourceRepository.PackageSource.Name, outcome.Failure)
            ),
        ];
        IReadOnlyList<string> failedSources =
        [
            .. namedOutcomes.Where(o => o.Failure is not null).Select(o => o.SourceName),
        ];
        IReadOnlyList<string> succeededSources =
        [
            .. namedOutcomes.Where(o => o.Failure is null).Select(o => o.SourceName),
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
            new AggregateException(outcomes.Select(outcome => outcome.Failure).OfType<Exception>())
        );
    }

    private async Task<(IReadOnlyList<PackageVersion> Found, Exception? Failure)> LoadPackagesFromSourceSafeAsync(
        SourceRepository sourceRepository,
        string packageId,
        CancellationToken cancellationToken
    )
    {
        try
        {
            IReadOnlyList<PackageVersion> found = await this.LoadPackagesFromSourceAsync(
                sourceRepository: sourceRepository,
                packageId: packageId,
                cancellationToken: cancellationToken
            );

            return (found, null);
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

            return ([], exception);
        }
    }
}
