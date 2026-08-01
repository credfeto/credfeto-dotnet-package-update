using System;
using Microsoft.Extensions.Logging;

namespace Credfeto.Package.Update.Services;

public sealed class LoggerProxy<TLogClass> : ILogger<TLogClass>
{
    private readonly IDiagnosticLogger _diagnosticLogger;

    public LoggerProxy(IDiagnosticLogger logger)
    {
        this._diagnosticLogger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        this._diagnosticLogger.Log(
            logLevel: logLevel,
            eventId: eventId,
            state: state,
            exception: exception,
            formatter: formatter
        );
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return this._diagnosticLogger.IsEnabled(logLevel);
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return this._diagnosticLogger.BeginScope(state);
    }
}
