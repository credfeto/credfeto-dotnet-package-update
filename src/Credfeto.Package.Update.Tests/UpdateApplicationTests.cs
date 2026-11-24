using System;
using System.IO;
using System.Threading.Tasks;
using Credfeto.Package.Update.Exceptions;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Package.Update.Tests;

public sealed class UpdateApplicationTests : TestBase
{
    [Fact]
    public async Task HelpReturnsSuccessAsync()
    {
        await using StringWriter output = new();

        int result = await this.RunAsync(output: output, "--help");

        Assert.Equal(expected: 0, actual: result);
    }

    [Fact]
    public async Task MissingRequiredOptionsReturnsErrorAsync()
    {
        await using StringWriter output = new();

        int result = await this.RunAsync(output: output, "-f", "/src");

        Assert.Equal(expected: UpdateApplication.ERROR, actual: result);
    }

    [Fact]
    public async Task FolderWithoutProjectsReturnsErrorAsync()
    {
        DirectoryInfo folder = Directory.CreateTempSubdirectory();

        try
        {
            await using StringWriter output = new();

            int result = await this.RunAsync(output: output, "-p", "Test.Package", "-f", folder.FullName);

            Assert.Equal(expected: UpdateApplication.ERROR, actual: result);
            Assert.Contains(
                expectedSubstring: "Total updates: 0",
                actualString: output.ToString(),
                comparisonType: StringComparison.Ordinal
            );
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task UnloadableProjectReportsUpdateFailureAsync()
    {
        DirectoryInfo folder = Directory.CreateTempSubdirectory();

        try
        {
            await File.WriteAllTextAsync(
                path: Path.Combine(folder.FullName, "Broken.csproj"),
                contents: "not xml",
                cancellationToken: this.CancellationToken()
            );
            await using StringWriter output = new();

            int result = await this.RunAsync(output: output, "-p", "Test.Package", "-f", folder.FullName);

            Assert.Equal(expected: UpdateApplication.ERROR, actual: result);
            Assert.Contains(
                expectedSubstring: nameof(PackageUpdateException),
                actualString: output.ToString(),
                comparisonType: StringComparison.Ordinal
            );
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task UnexpectedExceptionIsReportedAsErrorAsync()
    {
        await using StringWriter output = new();

        int result = await this.RunAsync(output: output, "-p", " ", "-f", "/src");

        Assert.Equal(expected: UpdateApplication.ERROR, actual: result);
        Assert.StartsWith(
            expectedStartString: "ERROR: ",
            actualString: output.ToString(),
            comparisonType: StringComparison.Ordinal
        );
    }

    private async Task<int> RunAsync(StringWriter output, params string[] args)
    {
        await using StringWriter error = new();

        return await UpdateApplication.RunAsync(
            args: args,
            gitHubEnvFilePath: null,
            output: output,
            error: error,
            cancellationToken: this.CancellationToken()
        );
    }
}
