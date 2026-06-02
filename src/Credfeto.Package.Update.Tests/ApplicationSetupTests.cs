using System;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Credfeto.Package.Update.Tests;

public sealed class ApplicationSetupTests : TestBase
{
    [Fact]
    public void SetupWithWarningsAsErrorsFalseReturnsNonNullServiceProvider()
    {
        IServiceProvider serviceProvider = ApplicationSetup.Setup(warningsAsErrors: false);

        Assert.NotNull(serviceProvider);
    }

    [Fact]
    public void SetupWithWarningsAsErrorsTrueReturnsNonNullServiceProvider()
    {
        IServiceProvider serviceProvider = ApplicationSetup.Setup(warningsAsErrors: true);

        Assert.NotNull(serviceProvider);
    }

    [Fact]
    public void SetupWithWarningsAsErrorsFalseCanResolveDiagnosticLogger()
    {
        IServiceProvider serviceProvider = ApplicationSetup.Setup(warningsAsErrors: false);

        IDiagnosticLogger diagnosticLogger = serviceProvider.GetRequiredService<IDiagnosticLogger>();

        Assert.NotNull(diagnosticLogger);
    }

    [Fact]
    public void SetupWithWarningsAsErrorsFalseCanResolvePackageUpdater()
    {
        IServiceProvider serviceProvider = ApplicationSetup.Setup(warningsAsErrors: false);

        IPackageUpdater packageUpdater = serviceProvider.GetRequiredService<IPackageUpdater>();

        Assert.NotNull(packageUpdater);
    }

    [Fact]
    public void SetupWithWarningsAsErrorsFalseCanResolvePackageCache()
    {
        IServiceProvider serviceProvider = ApplicationSetup.Setup(warningsAsErrors: false);

        IPackageCache packageCache = serviceProvider.GetRequiredService<IPackageCache>();

        Assert.NotNull(packageCache);
    }
}
