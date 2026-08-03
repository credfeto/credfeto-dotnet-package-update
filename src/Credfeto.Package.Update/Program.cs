using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Credfeto.Package.Exceptions;
using Credfeto.Package.Update.Exceptions;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.Package.Update;

internal static class Program
{
    private const int SUCCESS = 0;
    private const int ERROR = -1;

    private static async Task<int> Main(string[] args)
    {
        Console.WriteLine($"{VersionInformation.Product} {VersionInformation.Version}");
        Console.WriteLine();

        try
        {
            ParserResult<Options> parser = await ParseOptionsAsync(args);

            return parser.Tag == ParserResultType.Parsed ? SUCCESS : ERROR;
        }
        catch (NoPackagesUpdatedException)
        {
            return ERROR;
        }
        catch (PackageUpdateException exception)
        {
            Console.WriteLine(exception.Message);

            return ERROR;
        }
        catch (UpdateFailedException exception)
        {
            Console.WriteLine(exception.Message);

            return ERROR;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"ERROR: {exception.Message}");

            if (exception.StackTrace is not null)
            {
                Console.WriteLine(exception.StackTrace);
            }

            return ERROR;
        }
    }

    private static Task<ParserResult<Options>> ParseOptionsAsync(string[] args)
    {
        return Parser.Default.ParseArguments<Options>(args).WithNotParsed(NotParsed).WithParsedAsync(ParsedOkAsync);
    }

    private static void NotParsed(IEnumerable<Error> errors)
    {
        Console.WriteLine("Errors:");

        foreach (Error error in errors)
        {
            Console.WriteLine($" * {error.Tag.GetName()}");
        }
    }

    private static async Task ParsedOkAsync(Options options)
    {
        if (!string.IsNullOrWhiteSpace(options.Folder) && !string.IsNullOrWhiteSpace(options.PackageId))
        {
            IServiceProvider services = ApplicationSetup.Setup(false);

            IPackageCache packageCache = services.GetRequiredService<IPackageCache>();

            if (!string.IsNullOrWhiteSpace(options.Cache) && File.Exists(options.Cache))
            {
                await packageCache.LoadAsync(fileName: options.Cache, cancellationToken: CancellationToken.None);
            }

            IDiagnosticLogger logging = services.GetRequiredService<IDiagnosticLogger>();
            IPackageUpdater packageUpdater = services.GetRequiredService<IPackageUpdater>();

            PackageUpdateConfiguration config = BuildConfiguration(
                packageId: options.PackageId,
                options.Exclude?.ToArray() ?? []
            );
            IReadOnlyList<PackageVersion> updatesMade = await packageUpdater.UpdateAsync(
                basePath: options.Folder,
                configuration: config,
                options.Source?.ToArray() ?? [],
                cancellationToken: CancellationToken.None
            );

            if (logging.IsErrored)
            {
                throw new PackageUpdateException();
            }

            Console.WriteLine($"Total updates: {updatesMade.Count}");

            if (!string.IsNullOrWhiteSpace(options.Cache))
            {
                await packageCache.SaveAsync(fileName: options.Cache, cancellationToken: CancellationToken.None);
            }

            if (updatesMade is [])
            {
                throw new NoPackagesUpdatedException();
            }

            OutputPackageUpdateTags(updatesMade);

            return;
        }

        throw new InvalidOptionsException();
    }

    private static PackageUpdateConfiguration BuildConfiguration(string packageId, IReadOnlyList<string> exclude)
    {
        PackageMatch packageMatch = ExtractSearchPackage(packageId);
        Console.WriteLine($"Including {packageMatch.PackageId} (Using Prefix match: {packageMatch.Prefix})");

        IReadOnlyList<PackageMatch> excludedPackages = GetExcludedPackages(exclude);

        return new(PackageMatch: packageMatch, ExcludedPackages: excludedPackages);
    }

    private static void OutputPackageUpdateTags(IReadOnlyList<PackageVersion> updated)
    {
        GitHubActionsOutputWriter.WritePackageUpdates(updated, Environment.GetEnvironmentVariable("GITHUB_ENV"));
    }

    private static IReadOnlyList<PackageMatch> GetExcludedPackages(IReadOnlyList<string> excludes)
    {
        if (excludes is [])
        {
            return [];
        }

        return [.. excludes.Select(GetExclusion)];

        static PackageMatch GetExclusion(string exclude)
        {
            PackageMatch packageMatch = ExtractSearchPackage(exclude);

            Console.WriteLine($"Excluding {packageMatch.PackageId} (Using Prefix match: {packageMatch.Prefix})");

            return packageMatch;
        }
    }

    private static PackageMatch ExtractSearchPackage(string exclude)
    {
        string[] parts = exclude.Split(separator: ':');

        return parts.Length == 2
            ? new(parts[0], StringComparer.OrdinalIgnoreCase.Equals(parts[1], y: "prefix"))
            : new(parts[0], Prefix: false);
    }
}
