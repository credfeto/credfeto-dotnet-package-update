using System;
using Credfeto.Package.Update.Services;
using FunFair.Test.Common;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Credfeto.Package.Update.Tests.Services;

public sealed class DiagnosticLoggerTests : TestBase
{
    private const string TEST_MESSAGE = "Test log message";

    private static Func<string, Exception?, string> SimpleFormatter => static (state, _) => state;

    [Fact]
    public void ErrorsIsZeroInitially()
    {
        DiagnosticLogger logger = new(warningsAsErrors: false);

        Assert.Equal(expected: 0, actual: logger.Errors);
    }

    [Fact]
    public void IsErroredIsFalseInitially()
    {
        DiagnosticLogger logger = new(warningsAsErrors: false);

        Assert.False(logger.IsErrored, "IsErrored should be false when no errors have been logged");
    }

    [Theory]
    [InlineData(LogLevel.Trace, true)]
    [InlineData(LogLevel.Debug, false)]
    [InlineData(LogLevel.Information, true)]
    [InlineData(LogLevel.Warning, true)]
    [InlineData(LogLevel.Error, true)]
    [InlineData(LogLevel.Critical, true)]
    [InlineData(LogLevel.None, true)]
    public void IsEnabledReturnsExpectedResultForLogLevel(LogLevel logLevel, bool expected)
    {
        DiagnosticLogger logger = new(warningsAsErrors: false);

        Assert.Equal(expected: expected, actual: logger.IsEnabled(logLevel));
    }

    [Fact]
    public void BeginScopeReturnsNonNullDisposable()
    {
        DiagnosticLogger logger = new(warningsAsErrors: false);

        IDisposable? scope = logger.BeginScope(state: "test scope");

        Assert.NotNull(scope);
    }

    [Fact]
    public void BeginScopeCanBeDisposedWithoutError()
    {
        DiagnosticLogger logger = new(warningsAsErrors: false);

        using IDisposable? scope = logger.BeginScope(state: "test scope");

        Assert.NotNull(scope);
    }

    [Fact]
    public void LogInformationDoesNotIncrementErrors()
    {
        DiagnosticLogger logger = new(warningsAsErrors: false);

        logger.Log(
            logLevel: LogLevel.Information,
            eventId: default,
            state: TEST_MESSAGE,
            exception: null,
            formatter: SimpleFormatter
        );

        Assert.Equal(expected: 0, actual: logger.Errors);
        Assert.False(logger.IsErrored, "IsErrored should be false after logging information");
    }

    [Fact]
    public void LogWarningWithoutWarningsAsErrorsDoesNotIncrementErrors()
    {
        DiagnosticLogger logger = new(warningsAsErrors: false);

        logger.Log(
            logLevel: LogLevel.Warning,
            eventId: default,
            state: TEST_MESSAGE,
            exception: null,
            formatter: SimpleFormatter
        );

        Assert.Equal(expected: 0, actual: logger.Errors);
        Assert.False(
            logger.IsErrored,
            "IsErrored should be false after logging a warning when warningsAsErrors is false"
        );
    }

    [Fact]
    public void LogWarningWithWarningsAsErrorsIncrementsErrors()
    {
        DiagnosticLogger logger = new(warningsAsErrors: true);

        logger.Log(
            logLevel: LogLevel.Warning,
            eventId: default,
            state: TEST_MESSAGE,
            exception: null,
            formatter: SimpleFormatter
        );

        Assert.Equal(expected: 1, actual: logger.Errors);
        Assert.True(logger.IsErrored, "IsErrored should be true after logging a warning when warningsAsErrors is true");
    }

    [Fact]
    public void LogErrorIncrementsErrors()
    {
        DiagnosticLogger logger = new(warningsAsErrors: false);

        logger.Log(
            logLevel: LogLevel.Error,
            eventId: default,
            state: TEST_MESSAGE,
            exception: null,
            formatter: SimpleFormatter
        );

        Assert.Equal(expected: 1, actual: logger.Errors);
        Assert.True(logger.IsErrored, "IsErrored should be true after logging an error");
    }

    [Fact]
    public void LogCriticalIncrementsErrors()
    {
        DiagnosticLogger logger = new(warningsAsErrors: false);

        logger.Log(
            logLevel: LogLevel.Critical,
            eventId: default,
            state: TEST_MESSAGE,
            exception: null,
            formatter: SimpleFormatter
        );

        Assert.Equal(expected: 1, actual: logger.Errors);
        Assert.True(logger.IsErrored, "IsErrored should be true after logging a critical message");
    }

    [Fact]
    public void LogDebugDoesNotIncrementErrors()
    {
        DiagnosticLogger logger = new(warningsAsErrors: false);

        logger.Log(
            logLevel: LogLevel.Debug,
            eventId: default,
            state: TEST_MESSAGE,
            exception: null,
            formatter: SimpleFormatter
        );

        Assert.Equal(expected: 0, actual: logger.Errors);
        Assert.False(logger.IsErrored, "IsErrored should be false after logging debug message");
    }

    [Fact]
    public void LogErrorWithExceptionIncrementsErrors()
    {
        DiagnosticLogger logger = new(warningsAsErrors: false);
        InvalidOperationException exception = new("Something went wrong");

        logger.Log(
            logLevel: LogLevel.Error,
            eventId: default,
            state: TEST_MESSAGE,
            exception: exception,
            formatter: SimpleFormatter
        );

        Assert.Equal(expected: 1, actual: logger.Errors);
        Assert.True(logger.IsErrored, "IsErrored should be true after logging an error with exception");
    }

    [Fact]
    public void MultipleErrorsAccumulate()
    {
        DiagnosticLogger logger = new(warningsAsErrors: false);

        logger.Log(
            logLevel: LogLevel.Error,
            eventId: default,
            state: TEST_MESSAGE,
            exception: null,
            formatter: SimpleFormatter
        );
        logger.Log(
            logLevel: LogLevel.Critical,
            eventId: default,
            state: TEST_MESSAGE,
            exception: null,
            formatter: SimpleFormatter
        );

        Assert.Equal(expected: 2, actual: logger.Errors);
    }

    [Fact]
    public void LogTraceDoesNotIncrementErrors()
    {
        DiagnosticLogger logger = new(warningsAsErrors: false);

        logger.Log(
            logLevel: LogLevel.Trace,
            eventId: default,
            state: TEST_MESSAGE,
            exception: null,
            formatter: SimpleFormatter
        );

        Assert.Equal(expected: 0, actual: logger.Errors);
        Assert.False(logger.IsErrored, "IsErrored should be false after logging a trace message");
    }

    [Fact]
    public void LogNoneDoesNotIncrementErrors()
    {
        DiagnosticLogger logger = new(warningsAsErrors: false);

        logger.Log(
            logLevel: LogLevel.None,
            eventId: default,
            state: TEST_MESSAGE,
            exception: null,
            formatter: SimpleFormatter
        );

        Assert.Equal(expected: 0, actual: logger.Errors);
        Assert.False(logger.IsErrored, "IsErrored should be false after logging with None level");
    }
}
