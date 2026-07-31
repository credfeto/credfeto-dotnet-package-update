using System;
using System.Collections.Generic;
using System.IO;

namespace Credfeto.Package.Update;

public static class GitHubActionsOutputWriter
{
    public static void WritePackageUpdates(IReadOnlyList<PackageVersion> updated, string? gitHubEnvFilePath)
    {
        if (updated is [])
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(gitHubEnvFilePath))
        {
            File.AppendAllLines(gitHubEnvFilePath, BuildLines(updated));
        }

        foreach (PackageVersion package in updated)
        {
            Console.WriteLine($"{package.PackageId}={package.Version}");
        }
    }

    private static IReadOnlyList<string> BuildLines(IReadOnlyList<PackageVersion> updated)
    {
        string[] lines = new string[updated.Count];

        for (int index = 0; index < updated.Count; ++index)
        {
            PackageVersion package = updated[index];
            lines[index] = $"{package.PackageId}={package.Version}";
        }

        return lines;
    }
}
