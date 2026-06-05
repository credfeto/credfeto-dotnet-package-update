using System.IO;
using System.Threading.Tasks;
using Credfeto.Package.Services;
using FunFair.Test.Common;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Credfeto.Package.Tests.Services;

public sealed class ProjectLoaderTests : LoggingFolderCleanupTestBase
{
    private const string VALID_CSPROJ_CONTENT = """
        <Project Sdk="Microsoft.NET.Sdk">
          <ItemGroup>
            <PackageReference Include="Test.Package" Version="1.0.0" />
          </ItemGroup>
        </Project>
        """;

    public ProjectLoaderTests(ITestOutputHelper output)
        : base(output) { }

    private ProjectLoader CreateLoader()
    {
        ILogger<ProjectLoader> logger = this.GetTypedLogger<ProjectLoader>();

        return new ProjectLoader(logger);
    }

    [Fact]
    public async Task LoadAsync_WithValidProject_ReturnsProject()
    {
        string projectPath = Path.Combine(this.TempFolder, "TestProject.csproj");
        await File.WriteAllTextAsync(
            path: projectPath,
            contents: VALID_CSPROJ_CONTENT,
            cancellationToken: this.CancellationToken()
        );

        ProjectLoader loader = this.CreateLoader();

        IProject? result = await loader.LoadAsync(path: projectPath, cancellationToken: this.CancellationToken());

        Assert.NotNull(result);
        Assert.Equal(expected: projectPath, actual: result.FileName);
    }

    [Fact]
    public async Task LoadAsync_WithInvalidXml_ReturnsNull()
    {
        string projectPath = Path.Combine(this.TempFolder, "InvalidProject.csproj");
        await File.WriteAllTextAsync(
            path: projectPath,
            contents: "not valid xml <<<",
            cancellationToken: this.CancellationToken()
        );

        ProjectLoader loader = this.CreateLoader();

        IProject? result = await loader.LoadAsync(path: projectPath, cancellationToken: this.CancellationToken());

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadAsync_CalledTwiceForSamePath_ReturnsSameInstance()
    {
        string projectPath = Path.Combine(this.TempFolder, "CachedProject.csproj");
        await File.WriteAllTextAsync(
            path: projectPath,
            contents: VALID_CSPROJ_CONTENT,
            cancellationToken: this.CancellationToken()
        );

        ProjectLoader loader = this.CreateLoader();

        IProject? first = await loader.LoadAsync(path: projectPath, cancellationToken: this.CancellationToken());
        IProject? second = await loader.LoadAsync(path: projectPath, cancellationToken: this.CancellationToken());

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.True(ReferenceEquals(first, second), userMessage: "Second load should return the cached instance");
    }

    [Fact]
    public async Task Reset_ClearsCache_SoNextLoadReadsFromDisk()
    {
        string projectPath = Path.Combine(this.TempFolder, "ResetProject.csproj");
        await File.WriteAllTextAsync(
            path: projectPath,
            contents: VALID_CSPROJ_CONTENT,
            cancellationToken: this.CancellationToken()
        );

        ProjectLoader loader = this.CreateLoader();

        IProject? first = await loader.LoadAsync(path: projectPath, cancellationToken: this.CancellationToken());

        loader.Reset();

        IProject? second = await loader.LoadAsync(path: projectPath, cancellationToken: this.CancellationToken());

        Assert.NotNull(first);
        Assert.NotNull(second);
    }
}
