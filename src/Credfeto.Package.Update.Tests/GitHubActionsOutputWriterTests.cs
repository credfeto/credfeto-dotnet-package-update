using System.Collections.Generic;
using System.IO;
using FunFair.Test.Common;
using NuGet.Versioning;
using Xunit;

namespace Credfeto.Package.Update.Tests;

public sealed class GitHubActionsOutputWriterTests : TestBase
{
    [Fact]
    public void WritePackageUpdatesWithFilePathAppendsExpectedLines()
    {
        string gitHubEnvFilePath = Path.GetTempFileName();

        try
        {
            IReadOnlyList<PackageVersion> updated =
            [
                new(packageId: "Test.Package", version: new NuGetVersion("1.2.3")),
                new(packageId: "Other.Package", version: new NuGetVersion("4.5.6")),
            ];

            GitHubActionsOutputWriter.WritePackageUpdates(updated: updated, gitHubEnvFilePath: gitHubEnvFilePath);

            string[] lines = File.ReadAllLines(gitHubEnvFilePath);

            Assert.Equal(expected: ["Test.Package=1.2.3", "Other.Package=4.5.6"], actual: lines);
        }
        finally
        {
            File.Delete(gitHubEnvFilePath);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void WritePackageUpdatesWithNullOrWhitespaceFilePathDoesNotThrow(string? gitHubEnvFilePath)
    {
        IReadOnlyList<PackageVersion> updated = [new(packageId: "Test.Package", version: new NuGetVersion("1.2.3"))];

        GitHubActionsOutputWriter.WritePackageUpdates(updated: updated, gitHubEnvFilePath: gitHubEnvFilePath);
    }

    [Fact]
    public void WritePackageUpdatesWithEmptyPackageListDoesNotCreateFile()
    {
        string gitHubEnvFilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        GitHubActionsOutputWriter.WritePackageUpdates(updated: [], gitHubEnvFilePath: gitHubEnvFilePath);

        Assert.False(
            condition: File.Exists(gitHubEnvFilePath),
            userMessage: "Expected no file to be created for an empty package list"
        );
    }
}
