using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Credfeto.Package.Services;
using FunFair.Test.Common;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;
using Xunit;

namespace Credfeto.Package.Tests.Services;

public sealed class PackageCacheTests : LoggingFolderCleanupTestBase
{
    public PackageCacheTests(ITestOutputHelper output)
        : base(output) { }

    private PackageCache CreateCache()
    {
        ILogger<PackageCache> logger = this.GetTypedLogger<PackageCache>();

        return new PackageCache(logger);
    }

    [Fact]
    public void GetAll_WhenEmpty_ReturnsEmptyList()
    {
        PackageCache cache = this.CreateCache();

        IReadOnlyList<PackageVersion> result = cache.GetAll();

        Assert.Empty(result);
    }

    [Fact]
    public void GetVersions_WhenEmpty_ReturnsEmptyList()
    {
        PackageCache cache = this.CreateCache();

        IReadOnlyList<PackageVersion> result = cache.GetVersions(["Test.Package"]);

        Assert.Empty(result);
    }

    [Fact]
    public void SetVersions_AddsNewPackage()
    {
        PackageCache cache = this.CreateCache();
        PackageVersion packageVersion = new(packageId: "Test.Package", version: new NuGetVersion("1.0.0"));

        cache.SetVersions([packageVersion]);

        IReadOnlyList<PackageVersion> result = cache.GetAll();

        Assert.Single(result);
        Assert.Equal(expected: "Test.Package", actual: result[0].PackageId);
        Assert.Equal(expected: new NuGetVersion("1.0.0"), actual: result[0].Version);
    }

    [Fact]
    public void GetVersions_WithKnownPackage_ReturnsMatchingPackage()
    {
        PackageCache cache = this.CreateCache();
        PackageVersion packageVersion = new(packageId: "Test.Package", version: new NuGetVersion("1.0.0"));

        cache.SetVersions([packageVersion]);

        IReadOnlyList<PackageVersion> result = cache.GetVersions(["Test.Package"]);

        Assert.Single(result);
        Assert.Equal(expected: "Test.Package", actual: result[0].PackageId);
    }

    [Fact]
    public void SetVersions_UpdatesExistingPackageWithHigherVersion()
    {
        PackageCache cache = this.CreateCache();
        PackageVersion v1 = new(packageId: "Test.Package", version: new NuGetVersion("1.0.0"));
        PackageVersion v2 = new(packageId: "Test.Package", version: new NuGetVersion("2.0.0"));

        cache.SetVersions([v1]);
        cache.SetVersions([v2]);

        IReadOnlyList<PackageVersion> result = cache.GetVersions(["Test.Package"]);

        Assert.Single(result);
        Assert.Equal(expected: new NuGetVersion("2.0.0"), actual: result[0].Version);
    }

    [Fact]
    public void SetVersions_DoesNotDowngradeExistingPackage()
    {
        PackageCache cache = this.CreateCache();
        PackageVersion v2 = new(packageId: "Test.Package", version: new NuGetVersion("2.0.0"));
        PackageVersion v1 = new(packageId: "Test.Package", version: new NuGetVersion("1.0.0"));

        cache.SetVersions([v2]);
        cache.SetVersions([v1]);

        IReadOnlyList<PackageVersion> result = cache.GetVersions(["Test.Package"]);

        Assert.Single(result);
        Assert.Equal(expected: new NuGetVersion("2.0.0"), actual: result[0].Version);
    }

    [Fact]
    public void Reset_ClearsCache()
    {
        PackageCache cache = this.CreateCache();
        PackageVersion packageVersion = new(packageId: "Test.Package", version: new NuGetVersion("1.0.0"));
        cache.SetVersions([packageVersion]);

        cache.Reset();

        IReadOnlyList<PackageVersion> result = cache.GetAll();
        Assert.Empty(result);
    }

    [Fact]
    public async Task SaveAsync_WhenNotChanged_DoesNotCreateFile()
    {
        PackageCache cache = this.CreateCache();
        string filePath = Path.Combine(this.TempFolder, "cache-not-changed.json");

        await cache.SaveAsync(fileName: filePath, cancellationToken: this.CancellationToken());

        Assert.False(File.Exists(filePath), userMessage: "File should not exist when cache has not been changed");
    }

    [Fact]
    public async Task SaveAsync_WhenChanged_CreatesFile()
    {
        PackageCache cache = this.CreateCache();
        string filePath = Path.Combine(this.TempFolder, "cache-changed.json");

        PackageVersion packageVersion = new(packageId: "Test.Package", version: new NuGetVersion("1.0.0"));
        cache.SetVersions([packageVersion]);

        await cache.SaveAsync(fileName: filePath, cancellationToken: this.CancellationToken());

        Assert.True(File.Exists(filePath), userMessage: "File should exist after saving changed cache");
    }

    [Fact]
    public async Task LoadAsync_FromValidFile_PopulatesCache()
    {
        PackageCache cache = this.CreateCache();
        string filePath = Path.Combine(this.TempFolder, "cache-load.json");
        await File.WriteAllTextAsync(
            path: filePath,
            contents: "{\"TestPackage\":\"1.0.0\"}",
            cancellationToken: this.CancellationToken()
        );

        await cache.LoadAsync(fileName: filePath, cancellationToken: this.CancellationToken());

        IReadOnlyList<PackageVersion> result = cache.GetAll();
        Assert.Single(result);
        Assert.Equal(expected: "TestPackage", actual: result[0].PackageId);
        Assert.Equal(expected: new NuGetVersion("1.0.0"), actual: result[0].Version);
    }

    [Fact]
    public async Task LoadAsync_ThenSaveAsync_RoundTrip()
    {
        string sourceFilePath = Path.Combine(this.TempFolder, "cache-source.json");
        string destFilePath = Path.Combine(this.TempFolder, "cache-dest.json");
        await File.WriteAllTextAsync(
            path: sourceFilePath,
            contents: "{\"TestPackage\":\"1.0.0\"}",
            cancellationToken: this.CancellationToken()
        );

        PackageCache cache = this.CreateCache();

        await cache.LoadAsync(fileName: sourceFilePath, cancellationToken: this.CancellationToken());

        // Mark as changed so SaveAsync will write
        cache.Reset();

        IReadOnlyList<PackageVersion> loaded = cache.GetAll();
        Assert.Empty(loaded);

        // Use a fresh cache for a proper round-trip test
        PackageCache saveCache = this.CreateCache();
        PackageVersion packageVersion = new(packageId: "TestPackage", version: new NuGetVersion("1.0.0"));
        saveCache.SetVersions([packageVersion]);

        await saveCache.SaveAsync(fileName: destFilePath, cancellationToken: this.CancellationToken());

        Assert.True(File.Exists(destFilePath), userMessage: "Destination file should exist after save");

        PackageCache loadCache = this.CreateCache();
        await loadCache.LoadAsync(fileName: destFilePath, cancellationToken: this.CancellationToken());

        IReadOnlyList<PackageVersion> result = loadCache.GetAll();
        Assert.Single(result);
        Assert.Equal(expected: "TestPackage", actual: result[0].PackageId);
        Assert.Equal(expected: new NuGetVersion("1.0.0"), actual: result[0].Version);
    }
}
