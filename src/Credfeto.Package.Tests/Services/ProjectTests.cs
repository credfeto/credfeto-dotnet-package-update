using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Credfeto.Package.Services;
using FunFair.Test.Common;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;
using Xunit;

namespace Credfeto.Package.Tests.Services;

public sealed class ProjectTests : LoggingFolderCleanupTestBase
{
    public ProjectTests(ITestOutputHelper output)
        : base(output) { }

    private ProjectLoader CreateLoader()
    {
        ILogger<ProjectLoader> logger = this.GetTypedLogger<ProjectLoader>();

        return new ProjectLoader(logger);
    }

    private async Task<IProject?> LoadProjectAsync(string content)
    {
        string path = Path.Combine(this.TempFolder, "Test.csproj");
        await File.WriteAllTextAsync(path: path, contents: content, cancellationToken: this.CancellationToken());

        return await this.CreateLoader().LoadAsync(path: path, cancellationToken: this.CancellationToken());
    }

    [Fact]
    public async Task Packages_WhenPackageReferenceHasMsBuildPropertyVersion_ReturnsEmpty()
    {
        IProject? project = await this.LoadProjectAsync(
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Some.Package" Version="$(SomePackageVersion)" />
              </ItemGroup>
            </Project>
            """
        );

        Assert.NotNull(project);
        Assert.Empty(project.Packages);
    }

    [Fact]
    public async Task Packages_WhenPackageReferenceHasVersionRange_ReturnsEmpty()
    {
        IProject? project = await this.LoadProjectAsync(
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Some.Package" Version="[1.2.3,2.0.0)" />
              </ItemGroup>
            </Project>
            """
        );

        Assert.NotNull(project);
        Assert.Empty(project.Packages);
    }

    [Fact]
    public async Task Packages_WhenPackageReferenceHasFloatingVersion_ReturnsEmpty()
    {
        IProject? project = await this.LoadProjectAsync(
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Some.Package" Version="1.0.*" />
              </ItemGroup>
            </Project>
            """
        );

        Assert.NotNull(project);
        Assert.Empty(project.Packages);
    }

    [Fact]
    public async Task Packages_WhenValidPackageReferenceAlongsideInvalidOne_ReturnsOnlyValid()
    {
        IProject? project = await this.LoadProjectAsync(
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Valid.Package" Version="2.3.4" />
                <PackageReference Include="Invalid.Package" Version="$(SomeVersion)" />
              </ItemGroup>
            </Project>
            """
        );

        Assert.NotNull(project);
        PackageVersion entry = Assert.Single(project.Packages);
        Assert.Equal(expected: "Valid.Package", actual: entry.PackageId);
        Assert.Equal(expected: new NuGetVersion("2.3.4"), actual: entry.Version);
    }

    [Fact]
    public async Task Packages_WhenSdkAttributeHasUnparseableVersion_ReturnsEmpty()
    {
        IProject? project = await this.LoadProjectAsync(
            """
            <Project Sdk="Some.Sdk/$(SdkVersion)">
            </Project>
            """
        );

        Assert.NotNull(project);
        Assert.Empty(project.Packages);
    }

    [Fact]
    public async Task Packages_WhenSdkAttributeHasValidVersion_ReturnsSdkPackage()
    {
        IProject? project = await this.LoadProjectAsync(
            """
            <Project Sdk="Some.Sdk/1.2.3">
            </Project>
            """
        );

        Assert.NotNull(project);
        PackageVersion entry = Assert.Single(project.Packages);
        Assert.Equal(expected: "Some.Sdk", actual: entry.PackageId);
        Assert.Equal(expected: new NuGetVersion("1.2.3"), actual: entry.Version);
    }

    [Fact]
    public async Task UpdatePackage_WhenPackagePresentWithHigherVersion_ReturnsTrueAndUpdatesReference()
    {
        IProject? project = await this.LoadProjectAsync(
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Some.Package" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """
        );

        Assert.NotNull(project);
        bool updated = project.UpdatePackage(new(packageId: "Some.Package", new NuGetVersion("2.0.0")));

        Assert.True(updated, userMessage: "Expected update to report a change");
        Assert.True(project.Changed, userMessage: "Expected project to be marked as changed");
        PackageVersion entry = Assert.Single(project.Packages);
        Assert.Equal(expected: new NuGetVersion("2.0.0"), actual: entry.Version);
    }

    [Fact]
    public async Task UpdatePackage_WhenPackagePresentWithLowerOrEqualVersion_ReturnsFalse()
    {
        IProject? project = await this.LoadProjectAsync(
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Some.Package" Version="2.0.0" />
              </ItemGroup>
            </Project>
            """
        );

        Assert.NotNull(project);
        bool updated = project.UpdatePackage(new(packageId: "Some.Package", new NuGetVersion("1.0.0")));

        Assert.False(updated, userMessage: "Expected no update to be reported for a non-upgrading version");
        Assert.False(project.Changed, userMessage: "Expected project to remain unchanged");
        PackageVersion entry = Assert.Single(project.Packages);
        Assert.Equal(expected: new NuGetVersion("2.0.0"), actual: entry.Version);
    }

    [Fact]
    public async Task UpdatePackage_WhenPackageNotPresent_ReturnsFalse()
    {
        IProject? project = await this.LoadProjectAsync(
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Other.Package" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """
        );

        Assert.NotNull(project);
        bool updated = project.UpdatePackage(new(packageId: "Some.Package", new NuGetVersion("2.0.0")));

        Assert.False(updated, userMessage: "Expected no update when the package is not referenced at all");
        Assert.False(project.Changed, userMessage: "Expected project to remain unchanged");
    }

    [Fact]
    public async Task UpdatePackage_WhenPackagePresentOnlyWithUnparseableVersion_ReturnsFalse()
    {
        IProject? project = await this.LoadProjectAsync(
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Some.Package" Version="$(SomePackageVersion)" />
              </ItemGroup>
            </Project>
            """
        );

        Assert.NotNull(project);
        bool updated = project.UpdatePackage(new(packageId: "Some.Package", new NuGetVersion("2.0.0")));

        Assert.False(updated, userMessage: "Expected no update when the only reference has an unparseable version");
        Assert.False(project.Changed, userMessage: "Expected project to remain unchanged");
    }

    [Fact]
    public async Task UpdatePackage_WhenPackagePresentOnlyViaSdkAttribute_ReturnsTrueAndUpdatesSdk()
    {
        IProject? project = await this.LoadProjectAsync(
            """
            <Project Sdk="Some.Sdk/1.2.3">
            </Project>
            """
        );

        Assert.NotNull(project);
        bool updated = project.UpdatePackage(new(packageId: "Some.Sdk", new NuGetVersion("1.3.0")));

        Assert.True(updated, userMessage: "Expected update to report a change");
        Assert.True(project.Changed, userMessage: "Expected project to be marked as changed");
        PackageVersion entry = Assert.Single(project.Packages);
        Assert.Equal(expected: new NuGetVersion("1.3.0"), actual: entry.Version);
    }
}
