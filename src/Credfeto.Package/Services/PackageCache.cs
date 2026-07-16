using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Package.SerializationContext;
using Credfeto.Package.Services.LoggingExtensions;
using Microsoft.Extensions.Logging;
using NonBlocking;
using NuGet.Versioning;

namespace Credfeto.Package.Services;

public sealed class PackageCache : IPackageCache
{
    private readonly ConcurrentDictionary<string, PackageVersion> _cache;
    private readonly ILogger<PackageCache> _logger;
    private readonly JsonTypeInfo<CacheItems> _typeInfo;
    private bool _changed;

    public PackageCache(ILogger<PackageCache> logger)
    {
        this._logger = logger;
        this._cache = new(StringComparer.OrdinalIgnoreCase);

        this._typeInfo =
            SettingsSerializationContext.Default.GetTypeInfo(typeof(CacheItems)) as JsonTypeInfo<CacheItems>
            ?? MissingConverter();
        this._changed = false;
    }

    public async ValueTask LoadAsync(string fileName, CancellationToken cancellationToken)
    {
        this._logger.LoadingCache(fileName);

        string content = await File.ReadAllTextAsync(path: fileName, cancellationToken: cancellationToken);

        CacheItems? packages = JsonSerializer.Deserialize(json: content, jsonTypeInfo: this._typeInfo);

        if (packages is not null)
        {
            foreach (
                (string packageId, string version) in packages.Cache.OrderBy(
                    keySelector: x => x.Key,
                    comparer: StringComparer.OrdinalIgnoreCase
                )
            )
            {
                this._logger.LoadedPackageVersionFromCache(packageId: packageId, version: version);
                this._cache.TryAdd(
                    key: packageId,
                    new PackageVersion(packageId: packageId, version: NuGetVersion.Parse(version))
                );
            }
        }
    }

    public async ValueTask SaveAsync(string fileName, CancellationToken cancellationToken)
    {
        if (!this._changed)
        {
            return;
        }

        this._logger.SavingCache(fileName);

        // key off the value's package id, not the dictionary key - the key text is frozen to whichever
        // source was merged first, while the value always carries the winning source's casing (see #595)
        CacheItems toWrite = new(
            this._cache.ToDictionary(
                keySelector: x => x.Value.PackageId,
                elementSelector: x => x.Value.Version.ToString(),
                comparer: StringComparer.OrdinalIgnoreCase
            )
        );

        string content = JsonSerializer.Serialize(value: toWrite, jsonTypeInfo: this._typeInfo);

        await File.WriteAllTextAsync(path: fileName, contents: content, cancellationToken: cancellationToken);
        this._changed = false;
    }

    public IReadOnlyList<PackageVersion> GetAll()
    {
        return BuildVersions(this._cache);
    }

    public IReadOnlyList<PackageVersion> GetVersions(IReadOnlyList<string> packageIds)
    {
        List<PackageVersion> found = new(packageIds.Count);

        foreach (string packageId in packageIds)
        {
            if (this._cache.TryGetValue(key: packageId, out PackageVersion? version))
            {
                found.Add(version);
            }
        }

        return found;
    }

    public void SetVersions(IReadOnlyList<PackageVersion> matching)
    {
        foreach (PackageVersion packageVersion in matching)
        {
            this.UpdateCache(packageVersion);
        }
    }

    public void Reset()
    {
        this._changed = true;
        this._cache.Clear();
    }

    private static IReadOnlyList<PackageVersion> BuildVersions(IEnumerable<KeyValuePair<string, PackageVersion>> source)
    {
        return [.. source.Select(x => x.Value)];
    }

    [DoesNotReturn]
    private static JsonTypeInfo<CacheItems> MissingConverter()
    {
        throw new JsonException("No converter found for type CacheItems");
    }

    private void UpdateCache(PackageVersion packageVersion)
    {
        if (this._cache.TryGetValue(key: packageVersion.PackageId, out PackageVersion? existing))
        {
            if (existing.Version < packageVersion.Version)
            {
                // plain indexer set, not TryUpdate's compare-and-swap: PackageVersion equality compares
                // PackageId only (ignoring Version), so a version-based CAS comparisonValue would no
                // longer mean what it looks like. Safe because SetVersions/UpdateCache is only ever
                // called sequentially from PackageUpdater.UpdateAsync - no concurrent writers.
                this._cache[packageVersion.PackageId] = packageVersion;

                this._logger.UpdatingPackageVersion(
                    packageId: packageVersion.PackageId,
                    existing: existing.Version,
                    version: packageVersion.Version
                );
                this._changed = true;
            }
        }
        else
        {
            // single-threaded per the comment above, so TryAdd cannot fail here - no need to guard it
            this._cache.TryAdd(key: packageVersion.PackageId, value: packageVersion);
            this._logger.AddingPackageToCache(packageId: packageVersion.PackageId, version: packageVersion.Version);
            this._changed = true;
        }
    }
}
