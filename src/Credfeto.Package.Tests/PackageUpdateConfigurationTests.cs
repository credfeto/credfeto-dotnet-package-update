using System.Collections.Generic;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Package.Tests;

public sealed class PackageUpdateConfigurationTests : TestBase
{
    [Fact]
    public void PackageMatchPropertyReturnsConstructorValue()
    {
        PackageMatch packageMatch = new(PackageId: "Test.Package", Prefix: false);
        PackageUpdateConfiguration config = new(PackageMatch: packageMatch, ExcludedPackages: []);

        Assert.Equal(expected: packageMatch, actual: config.PackageMatch);
    }

    [Fact]
    public void ExcludedPackagesPropertyIsEmptyWhenNoneProvided()
    {
        PackageMatch packageMatch = new(PackageId: "Test.Package", Prefix: false);
        PackageUpdateConfiguration config = new(PackageMatch: packageMatch, ExcludedPackages: []);

        Assert.Empty(config.ExcludedPackages);
    }

    [Fact]
    public void ExcludedPackagesPropertyReturnsConstructorValue()
    {
        PackageMatch packageMatch = new(PackageId: "Test.Package", Prefix: false);
        PackageMatch excluded1 = new(PackageId: "Excluded.Package", Prefix: false);
        PackageMatch excluded2 = new(PackageId: "Other.Excluded", Prefix: true);

        IReadOnlyList<PackageMatch> excludedPackages = [excluded1, excluded2];

        PackageUpdateConfiguration config = new(PackageMatch: packageMatch, ExcludedPackages: excludedPackages);

        Assert.Equal(expected: 2, actual: config.ExcludedPackages.Count);
        Assert.Contains(excluded1, config.ExcludedPackages);
        Assert.Contains(excluded2, config.ExcludedPackages);
    }

    [Fact]
    public void PackageMatchAndExcludedPackagesPropertyAreIndependent()
    {
        PackageMatch packageMatch = new(PackageId: "Test.Package", Prefix: true);
        PackageMatch excluded = new(PackageId: "Test.Package.Excluded", Prefix: false);

        PackageUpdateConfiguration config = new(PackageMatch: packageMatch, ExcludedPackages: [excluded]);

        Assert.Equal(expected: packageMatch, actual: config.PackageMatch);
        Assert.Single(config.ExcludedPackages);
    }
}
