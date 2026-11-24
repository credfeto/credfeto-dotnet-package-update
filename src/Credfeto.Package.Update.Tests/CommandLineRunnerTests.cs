using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Package.Update.Tests;

public sealed class CommandLineRunnerTests : TestBase
{
    private const int HANDLER_RESULT = 42;

    [Fact]
    public async Task LongNamesAreParsedAsync()
    {
        OptionsCapture capture = new();

        int result = await this.RunAsync(
            capture: capture,
            "--package-id",
            "Test.Package",
            "--folder",
            "/src",
            "--cache",
            "cache.json",
            "--source",
            "https://one.example/index.json",
            "--exclude",
            "Excluded.Package"
        );

        Assert.Equal(expected: HANDLER_RESULT, actual: result);
        Options options = capture.Required();
        Assert.Equal(expected: "Test.Package", actual: options.PackageId);
        Assert.Equal(expected: "/src", actual: options.Folder);
        Assert.Equal(expected: "cache.json", actual: options.Cache);
        Assert.Equal(expected: ["https://one.example/index.json"], actual: options.Source);
        Assert.Equal(expected: ["Excluded.Package"], actual: options.Exclude);
    }

    [Fact]
    public async Task ShortNamesWithMultipleValuesPerTokenAreParsedAsync()
    {
        OptionsCapture capture = new();

        int result = await this.RunAsync(
            capture: capture,
            "-p",
            "Test.Package:prefix",
            "-f",
            "/src",
            "-c",
            "cache.json",
            "-s",
            "https://one.example/index.json",
            "https://two.example/index.json",
            "-x",
            "Excluded.One",
            "Excluded.Two:prefix"
        );

        Assert.Equal(expected: HANDLER_RESULT, actual: result);
        Options options = capture.Required();
        Assert.Equal(expected: "Test.Package:prefix", actual: options.PackageId);
        Assert.Equal(expected: "/src", actual: options.Folder);
        Assert.Equal(expected: "cache.json", actual: options.Cache);
        Assert.Equal(
            expected: ["https://one.example/index.json", "https://two.example/index.json"],
            actual: options.Source
        );
        Assert.Equal(expected: ["Excluded.One", "Excluded.Two:prefix"], actual: options.Exclude);
    }

    [Fact]
    public async Task RepeatedOptionsAreCombinedAsync()
    {
        OptionsCapture capture = new();

        int result = await this.RunAsync(
            capture: capture,
            "-p",
            "Test.Package",
            "-f",
            "/src",
            "-s",
            "https://one.example/index.json",
            "-s",
            "https://two.example/index.json"
        );

        Assert.Equal(expected: HANDLER_RESULT, actual: result);
        Assert.Equal(
            expected: ["https://one.example/index.json", "https://two.example/index.json"],
            actual: capture.Required().Source
        );
    }

    [Fact]
    public async Task OptionalOptionsDefaultWhenAbsentAsync()
    {
        OptionsCapture capture = new();

        int result = await this.RunAsync(capture: capture, "-p", "Test.Package", "-f", "/src");

        Assert.Equal(expected: HANDLER_RESULT, actual: result);
        Options options = capture.Required();
        Assert.Null(options.Cache);
        Assert.Empty(options.Source);
        Assert.Empty(options.Exclude);
    }

    [Theory]
    [InlineData("-f", "/src")]
    [InlineData("-p", "Test.Package")]
    public async Task MissingRequiredOptionReturnsParseErrorAsync(string name, string value)
    {
        OptionsCapture capture = new();
        await using StringWriter output = new();
        await using StringWriter error = new();

        int result = await CommandLineRunner.RunAsync(
            args: [name, value],
            handler: capture.Handler,
            output: output,
            error: error,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: CommandLineRunner.PARSE_ERROR, actual: result);
        Assert.Null(capture.Options);
        Assert.StartsWith(
            expectedStartString: "Errors:",
            actualString: output.ToString(),
            comparisonType: StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task UnknownOptionReturnsParseErrorAsync()
    {
        OptionsCapture capture = new();

        int result = await this.RunAsync(capture: capture, "-p", "Test.Package", "-f", "/src", "--unknown");

        Assert.Equal(expected: CommandLineRunner.PARSE_ERROR, actual: result);
        Assert.Null(capture.Options);
    }

    [Fact]
    public async Task HelpReturnsSuccessWithoutRunningHandlerAsync()
    {
        OptionsCapture capture = new();
        await using StringWriter output = new();
        await using StringWriter error = new();

        int result = await CommandLineRunner.RunAsync(
            args: ["--help"],
            handler: capture.Handler,
            output: output,
            error: error,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: 0, actual: result);
        Assert.Null(capture.Options);
        Assert.Contains(
            expectedSubstring: "--package-id",
            actualString: output.ToString(),
            comparisonType: StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task HandlerExceptionsPropagateToCallerAsync()
    {
        await using StringWriter output = new();
        await using StringWriter error = new();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CommandLineRunner.RunAsync(
                args: ["-p", "Test.Package", "-f", "/src"],
                handler: static (_, _) => throw new InvalidOperationException("handler failed"),
                output: output,
                error: error,
                cancellationToken: this.CancellationToken()
            )
        );
    }

    private async Task<int> RunAsync(OptionsCapture capture, params string[] args)
    {
        await using StringWriter output = new();
        await using StringWriter error = new();

        return await CommandLineRunner.RunAsync(
            args: args,
            handler: capture.Handler,
            output: output,
            error: error,
            cancellationToken: this.CancellationToken()
        );
    }

    private sealed class OptionsCapture
    {
        public OptionsCapture()
        {
            this.Handler = (options, _) =>
            {
                this.Options = options;

                return Task.FromResult(HANDLER_RESULT);
            };
        }

        public Options? Options { get; private set; }

        public Func<Options, CancellationToken, Task<int>> Handler { get; }

        public Options Required()
        {
            return Assert.IsType<Options>(this.Options);
        }
    }
}
