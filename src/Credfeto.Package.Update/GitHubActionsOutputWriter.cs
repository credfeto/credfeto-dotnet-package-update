using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Credfeto.Package.Update;

public static class GitHubActionsOutputWriter
{
    public static void WritePackageUpdates(IReadOnlyList<PackageVersion> updated, string? gitHubEnvFilePath)
    {
        if (updated is [])
        {
            return;
        }

        string[] lines = [.. updated.Select(static package => $"{package.PackageId}={package.Version}")];

        if (!string.IsNullOrWhiteSpace(gitHubEnvFilePath))
        {
            File.AppendAllLines(gitHubEnvFilePath, lines);
        }

        foreach (string line in lines)
        {
            Console.WriteLine(line);
        }
    }
}
