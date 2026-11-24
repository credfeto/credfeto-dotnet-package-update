using System;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Package.Update;

internal static class Program
{
    private static Task<int> Main(string[] args)
    {
        Console.WriteLine($"{VersionInformation.Product} {VersionInformation.Version}");
        Console.WriteLine();

        return UpdateApplication.RunAsync(
            args: args,
            gitHubEnvFilePath: Environment.GetEnvironmentVariable("GITHUB_ENV"),
            output: Console.Out,
            error: Console.Error,
            cancellationToken: CancellationToken.None
        );
    }
}
