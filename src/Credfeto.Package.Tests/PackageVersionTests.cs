using System;
using FunFair.Test.Common;
using NuGet.Versioning;
using Xunit;

namespace Credfeto.Package.Tests;

public sealed class PackageVersionTests : EquatableObjectTestBase<PackageVersion>
{
    public PackageVersionTests()
        : base(
            zeroObject: new PackageVersion(packageId: "Zero.Package", version: new NuGetVersion("0.0.0")),
            value1: new PackageVersion(packageId: "Test.Package", version: new NuGetVersion("1.0.0")),
            equivalentToValue1: new PackageVersion(packageId: "Test.Package", version: new NuGetVersion("2.0.0"))
        ) { }

    protected override bool OperatorEquals(PackageVersion? x, PackageVersion? y)
    {
        return x == y;
    }

    protected override bool OperatorNotEquals(PackageVersion? x, PackageVersion? y)
    {
        return x != y;
    }

    public static TheoryData<string, Action<PackageVersionTests>> BaseCaseData() =>
        BuildDispatcherCases<PackageVersionTests>().ToTheoryData();

    [Theory]
    [MemberData(nameof(BaseCaseData))]
    public void CommonTests(string name, Action<PackageVersionTests> action)
    {
        Assert.NotEmpty(name);
        action(this);
    }
}
