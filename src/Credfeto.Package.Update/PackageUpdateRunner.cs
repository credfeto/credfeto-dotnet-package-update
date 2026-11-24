using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Package.Update.Exceptions;

namespace Credfeto.Package.Update;

public sealed class PackageUpdateRunner
{
    public const int SUCCESS = 0;

    private readonly IDiagnosticLogger _logger;
    private readonly IPackageCache _packageCache;
    private readonly IPackageUpdater _packageUpdater;

    public PackageUpdateRunner(IPackageCache packageCache, IDiagnosticLogger logger, IPackageUpdater packageUpdater)
    {
        this._packageCache = packageCache;
        this._logger = logger;
        this._packageUpdater = packageUpdater;
    }

    public async Task<int> UpdateAsync(
        Options options,
        string? gitHubEnvFilePath,
        TextWriter output,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(options.Folder) || string.IsNullOrWhiteSpace(options.PackageId))
        {
            throw new InvalidOptionsException();
        }

        if (!string.IsNullOrWhiteSpace(options.Cache) && File.Exists(options.Cache))
        {
            await this._packageCache.LoadAsync(fileName: options.Cache, cancellationToken: cancellationToken);
        }

        PackageUpdateConfiguration configuration = await BuildConfigurationAsync(
            options: options,
            output: output,
            cancellationToken: cancellationToken
        );

        IReadOnlyList<PackageVersion> updatesMade = await this._packageUpdater.UpdateAsync(
            basePath: options.Folder,
            configuration: configuration,
            packageSources: options.Source,
            cancellationToken: cancellationToken
        );

        if (this._logger.IsErrored)
        {
            throw new PackageUpdateException();
        }

        await WriteLineAsync(
            output: output,
            message: $"Total updates: {updatesMade.Count}",
            cancellationToken: cancellationToken
        );

        if (!string.IsNullOrWhiteSpace(options.Cache))
        {
            await this._packageCache.SaveAsync(fileName: options.Cache, cancellationToken: cancellationToken);
        }

        if (updatesMade is [])
        {
            throw new NoPackagesUpdatedException();
        }

        GitHubActionsOutputWriter.WritePackageUpdates(updated: updatesMade, gitHubEnvFilePath: gitHubEnvFilePath);

        return SUCCESS;
    }

    private static async ValueTask<PackageUpdateConfiguration> BuildConfigurationAsync(
        Options options,
        TextWriter output,
        CancellationToken cancellationToken
    )
    {
        PackageMatch packageMatch = ExtractSearchPackage(options.PackageId);
        await WriteLineAsync(
            output: output,
            message: $"Including {packageMatch.PackageId} (Using Prefix match: {packageMatch.Prefix})",
            cancellationToken: cancellationToken
        );

        IReadOnlyList<PackageMatch> excludedPackages = [.. options.Exclude.Select(ExtractSearchPackage)];

        foreach (PackageMatch excluded in excludedPackages)
        {
            await WriteLineAsync(
                output: output,
                message: $"Excluding {excluded.PackageId} (Using Prefix match: {excluded.Prefix})",
                cancellationToken: cancellationToken
            );
        }

        return new(PackageMatch: packageMatch, ExcludedPackages: excludedPackages);
    }

    private static PackageMatch ExtractSearchPackage(string package)
    {
        string[] parts = package.Split(separator: ':');

        return parts.Length == 2
            ? new(parts[0], StringComparer.OrdinalIgnoreCase.Equals(parts[1], y: "prefix"))
            : new(parts[0], Prefix: false);
    }

    private static Task WriteLineAsync(TextWriter output, string message, in CancellationToken cancellationToken)
    {
        return output.WriteLineAsync(buffer: message.AsMemory(), cancellationToken: cancellationToken);
    }
}
