using FunFair.Test.Common;
using NuGet.Versioning;
using Xunit;

namespace Credfeto.Package.Tests;

public sealed class PackageMatchTests : TestBase
{
    private static PackageVersion MakePackageVersion(string packageId)
    {
        return new PackageVersion(packageId: packageId, version: new NuGetVersion("1.0.0"));
    }

    [Theory]
    [InlineData("Test.Package", "Test.Package", true)]
    [InlineData("Test.Package", "TEST.PACKAGE", true)]
    [InlineData("Test.Package", "test.package", true)]
    [InlineData("Test.Package", "Other.Package", false)]
    [InlineData("Test.Package", "Test.PackageExtra", false)]
    public void ExactMatch_ReturnsExpected(string packageId, string candidateId, bool expected)
    {
        PackageMatch match = new(PackageId: packageId, Prefix: false);
        PackageVersion packageVersion = MakePackageVersion(candidateId);

        bool result = match.IsMatchingPackage(packageVersion);

        Assert.Equal(expected: expected, actual: result);
    }

    [Theory]
    [InlineData("Test.Package", "Test.Package", true)]
    [InlineData("Test.Package", "TEST.PACKAGE", true)]
    [InlineData("Test.Package", "test.package", true)]
    [InlineData("Test.Package", "Test.Package.Sub", true)]
    [InlineData("Test.Package", "TEST.PACKAGE.SUB", true)]
    [InlineData("Test.Package", "test.package.sub", true)]
    [InlineData("Test.Package", "Test.PackageExtra", false)]
    [InlineData("Test.Package", "Other.Package", false)]
    public void PrefixMatch_ReturnsExpected(string packageId, string candidateId, bool expected)
    {
        PackageMatch match = new(PackageId: packageId, Prefix: true);
        PackageVersion packageVersion = MakePackageVersion(candidateId);

        bool result = match.IsMatchingPackage(packageVersion);

        Assert.Equal(expected: expected, actual: result);
    }
}
