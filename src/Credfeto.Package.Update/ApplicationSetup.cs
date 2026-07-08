using System;
using Credfeto.Package.Update.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Credfeto.Package.Update;

public static class ApplicationSetup
{
    public static IServiceProvider Setup(bool warningsAsErrors)
    {
        DiagnosticLogger logger = new(warningsAsErrors);

        return new ServiceCollection()
            .AddSingleton<ILogger>(logger)
            .AddSingleton<IDiagnosticLogger>(logger)
            .AddSingleton(typeof(ILogger<>), typeof(LoggerProxy<>))
            .AddPackageUpdater()
            .BuildServiceProvider();
    }
}
