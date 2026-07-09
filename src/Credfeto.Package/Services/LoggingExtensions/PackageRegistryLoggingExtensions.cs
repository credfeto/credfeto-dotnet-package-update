using System;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace Credfeto.Package.Services.LoggingExtensions;

internal static partial class PackageRegistryLoggingExtensions
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Found package {packageId} version {version} in {source}"
    )]
    public static partial void FoundPackageInSource(
        this ILogger<PackageRegistry> logger,
        string packageId,
        NuGetVersion version,
        string source
    );

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Enumerating matching package versions for {packageId}..."
    )]
    public static partial void EnumeratingPackageVersions(this ILogger<PackageRegistry> logger, string packageId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Package source {source} failed while looking up {packageId}: {message}"
    )]
    public static partial void PackageSourceQueryFailed(
        this ILogger<PackageRegistry> logger,
        string source,
        string packageId,
        string message,
        Exception exception
    );

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "All {sourceCount} package sources failed while looking up {packageId}"
    )]
    public static partial void AllPackageSourcesFailed(
        this ILogger<PackageRegistry> logger,
        int sourceCount,
        string packageId
    );
}
