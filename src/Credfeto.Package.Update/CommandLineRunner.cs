using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Package.Update;

public static class CommandLineRunner
{
    public const int PARSE_ERROR = -1;

    public static async Task<int> RunAsync(
        IReadOnlyList<string> args,
        Func<Options, CancellationToken, Task<int>> handler,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken
    )
    {
        ParseResult parseResult = CreateRootCommand(handler).Parse(args);

        if (parseResult.Errors is [_, ..])
        {
            await WriteErrorsAsync(output: output, errors: parseResult.Errors, cancellationToken: cancellationToken);

            return PARSE_ERROR;
        }

        // Exceptions thrown by the handler must reach the caller so it can map them to exit codes.
        InvocationConfiguration configuration = new()
        {
            EnableDefaultExceptionHandler = false,
            Output = output,
            Error = error,
        };

        return await parseResult.InvokeAsync(configuration: configuration, cancellationToken: cancellationToken);
    }

    private static RootCommand CreateRootCommand(Func<Options, CancellationToken, Task<int>> handler)
    {
        Option<string> packageId = new("--package-id", "-p")
        {
            Description = "Package Id to check for updates",
            Required = true,
        };
        Option<string> folder = new("--folder", "-f") { Description = "Folder containing projects", Required = true };
        Option<string> cache = new("--cache", "-c") { Description = "cache file" };
        Option<string[]> source = new("--source", "-s")
        {
            Description = "Urls to additional NuGet feeds to load",
            AllowMultipleArgumentsPerToken = true,
            DefaultValueFactory = _ => [],
        };
        Option<string[]> exclude = new("--exclude", "-x")
        {
            Description = "Package Ids to exclude from the update",
            AllowMultipleArgumentsPerToken = true,
            DefaultValueFactory = _ => [],
        };

        RootCommand rootCommand = [with("Scriptable update package tool"), packageId, folder, cache, source, exclude];

        rootCommand.SetAction(
            (parsed, cancellationToken) =>
                handler(
                    new(
                        PackageId: parsed.GetRequiredValue(packageId),
                        Folder: parsed.GetRequiredValue(folder),
                        Cache: parsed.GetValue(cache),
                        Source: parsed.GetRequiredValue(source),
                        Exclude: parsed.GetRequiredValue(exclude)
                    ),
                    cancellationToken
                )
        );

        return rootCommand;
    }

    private static async ValueTask WriteErrorsAsync(
        TextWriter output,
        IReadOnlyList<ParseError> errors,
        CancellationToken cancellationToken
    )
    {
        await output.WriteLineAsync(buffer: "Errors:".AsMemory(), cancellationToken: cancellationToken);

        foreach (ParseError parseError in errors)
        {
            await output.WriteLineAsync(
                buffer: $" * {parseError.Message}".AsMemory(),
                cancellationToken: cancellationToken
            );
        }
    }
}
