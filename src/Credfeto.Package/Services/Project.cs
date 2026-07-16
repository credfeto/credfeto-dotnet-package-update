using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Credfeto.Extensions.Linq;
using NuGet.Versioning;

namespace Credfeto.Package.Services;

internal sealed class Project : IProject
{
    private const byte NewLine = (byte)'\n';

    private static readonly XmlWriterSettings WriterSettings = new()
    {
        Async = true,
        Indent = true,
        IndentChars = "  ",
        OmitXmlDeclaration = true,
        Encoding = Encoding.UTF8,
        NewLineHandling = NewLineHandling.None,
        NewLineChars = "\n",
        NewLineOnAttributes = false,
        NamespaceHandling = NamespaceHandling.OmitDuplicates,
        CloseOutput = true,
    };

    private readonly XmlDocument _doc;

    public Project(string fileName, XmlDocument doc)
    {
        this.FileName = fileName;
        this._doc = doc;
        this.Changed = false;
    }

    public string FileName { get; }

    public IReadOnlyList<PackageVersion> Packages => this.GetCurrentPackageVersions();

    public bool Changed { get; private set; }

    public bool UpdatePackage(PackageVersion package)
    {
        if (!this.HasPackageReference(package.PackageId))
        {
            return false;
        }

        bool updated = false;
        updated |= this.UpdatePackageFromReference(package);
        updated |= this.UpdatePackageFromSdk(package);

        return updated;
    }

    public bool Save()
    {
        if (!this.Changed)
        {
            return false;
        }

        using (MemoryStream stream = new())
        {
            using (XmlWriter writer = XmlWriter.Create(output: stream, settings: WriterSettings))
            {
                this._doc.Save(writer);
            }

            File.WriteAllBytes(path: this.FileName, bytes: EnsureSingleTrailingNewLine(stream.ToArray()));
        }

        // explicitly mark as not saved
        this.Changed = false;

        return true;
    }

    private static byte[] EnsureSingleTrailingNewLine(byte[] content)
    {
        int end = content.Length;

        while (end > 0 && (content[end - 1] == NewLine || content[end - 1] == (byte)'\r'))
        {
            --end;
        }

        byte[] trimmed = new byte[end + 1];
        Array.Copy(sourceArray: content, destinationArray: trimmed, length: end);
        trimmed[end] = NewLine;

        return trimmed;
    }

    private bool UpdatePackageFromReference(PackageVersion package)
    {
        int updates = 0;
        IEnumerable<XmlElement> references = this.GetPackageReferences();

        foreach (XmlElement node in references)
        {
            string packageId = node.GetAttribute("Include");
            string version = node.GetAttribute("Version");

            if (string.IsNullOrWhiteSpace(packageId) || string.IsNullOrWhiteSpace(version))
            {
                continue;
            }

            if (ShouldUpdate(package: package, packageId: packageId, version: version))
            {
                node.SetAttribute(name: "Include", value: package.PackageId);
                node.SetAttribute(name: "Version", package.Version.ToString());
                ++updates;
                this.Changed = true;
            }
        }

        return updates > 0;
    }

    private bool UpdatePackageFromSdk(PackageVersion package)
    {
        if (this._doc.SelectSingleNode("/Project") is not XmlElement project)
        {
            return false;
        }

        IReadOnlyList<string> sdk = project.GetAttribute("Sdk").Split("/");

        if (sdk.Count != 2)
        {
            return false;
        }

        if (ShouldUpdate(package: package, sdk[0], sdk[1]))
        {
            project.SetAttribute(name: "Sdk", $"{package.PackageId}/{package.Version}");
            this.Changed = true;

            return true;
        }

        return false;
    }

    private static bool ShouldUpdate(PackageVersion package, string packageId, string version)
    {
        if (!StringComparer.OrdinalIgnoreCase.Equals(x: packageId, y: package.PackageId))
        {
            return false;
        }

        if (!NuGetVersion.TryParse(value: version, out NuGetVersion? existingVersion))
        {
            return false;
        }

        if (package.Version <= existingVersion)
        {
            return false;
        }

        return true;
    }

    private bool HasPackageReference(string packageId)
    {
        bool hasReference = this.GetPackageReferences()
            .Any(node => StringComparer.OrdinalIgnoreCase.Equals(x: node.GetAttribute("Include"), y: packageId));

        return hasReference || this.HasSdkReference(packageId);
    }

    private bool HasSdkReference(string packageId)
    {
        if (this._doc.SelectSingleNode("/Project") is not XmlElement project)
        {
            return false;
        }

        IReadOnlyList<string> sdk = project.GetAttribute("Sdk").Split("/");

        return sdk.Count == 2 && StringComparer.OrdinalIgnoreCase.Equals(x: sdk[0], y: packageId);
    }

    private IReadOnlyList<PackageVersion> GetCurrentPackageVersions()
    {
        IEnumerable<PackageVersion> refPackages = this.GetPackagesFromReferences();
        IEnumerable<PackageVersion> sdkPackages = this.GetPackagesFromSdk();

        return
        [
            .. refPackages
                .Concat(sdkPackages)
                .OrderBy(keySelector: x => x.PackageId, comparer: StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Version),
        ];
    }

    private IEnumerable<XmlElement> GetPackageReferences()
    {
        return this._doc.SelectNodes("/Project/ItemGroup/PackageReference")?.OfType<XmlElement>().RemoveNulls() ?? [];
    }

    private IEnumerable<PackageVersion> GetPackagesFromReferences()
    {
        IEnumerable<XmlElement> references = this.GetPackageReferences();

        foreach (XmlElement node in references)
        {
            string packageId = node.GetAttribute("Include");
            string version = node.GetAttribute("Version");

            if (string.IsNullOrWhiteSpace(packageId) || string.IsNullOrWhiteSpace(version))
            {
                continue;
            }

            if (!NuGetVersion.TryParse(value: version, out NuGetVersion? parsedVersion))
            {
                continue;
            }

            yield return new(packageId: packageId, parsedVersion);
        }
    }

    private IEnumerable<PackageVersion> GetPackagesFromSdk()
    {
        if (this._doc.SelectSingleNode("/Project") is not XmlElement project)
        {
            yield break;
        }

        IReadOnlyList<string> sdk = project.GetAttribute("Sdk").Split("/");

        if (sdk.Count == 2 && NuGetVersion.TryParse(value: sdk[1], out NuGetVersion? parsedVersion))
        {
            yield return new(sdk[0], parsedVersion);
        }
    }
}
