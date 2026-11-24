using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Package.Exceptions;
using Credfeto.Package.Update.Exceptions;
using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.Package.Update;

public static class UpdateApplication
{
    public const int ERROR = -1;

    public static async Task<int> RunAsync(
        IReadOnlyList<string> args,
        string? gitHubEnvFilePath,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await CommandLineRunner.RunAsync(
                args: args,
                handler: (options, token) =>
                    UpdateAsync(
                        options: options,
                        gitHubEnvFilePath: gitHubEnvFilePath,
                        output: output,
                        cancellationToken: token
                    ),
                output: output,
                error: error,
                cancellationToken: cancellationToken
            );
        }
        catch (NoPackagesUpdatedException)
        {
            return ERROR;
        }
        catch (Exception exception) when (exception is PackageUpdateException or UpdateFailedException)
        {
            return await ReportAsync(output: output, message: exception.Message, cancellationToken: cancellationToken);
        }
        catch (Exception exception)
        {
            return await ReportAsync(
                output: output,
                message: $"ERROR: {exception.Message}{Environment.NewLine}{exception.StackTrace}",
                cancellationToken: cancellationToken
            );
        }
    }

    private static Task<int> UpdateAsync(
        Options options,
        string? gitHubEnvFilePath,
        TextWriter output,
        in CancellationToken cancellationToken
    )
    {
        IServiceProvider services = ApplicationSetup.Setup(false);

        PackageUpdateRunner runner = new(
            packageCache: services.GetRequiredService<IPackageCache>(),
            logger: services.GetRequiredService<IDiagnosticLogger>(),
            packageUpdater: services.GetRequiredService<IPackageUpdater>()
        );

        return runner.UpdateAsync(
            options: options,
            gitHubEnvFilePath: gitHubEnvFilePath,
            output: output,
            cancellationToken: cancellationToken
        );
    }

    private static async Task<int> ReportAsync(TextWriter output, string message, CancellationToken cancellationToken)
    {
        await output.WriteLineAsync(buffer: message.AsMemory(), cancellationToken: cancellationToken);

        return ERROR;
    }
}
