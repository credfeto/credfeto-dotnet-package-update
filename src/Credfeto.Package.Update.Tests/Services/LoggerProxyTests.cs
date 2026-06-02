using System;
using Credfeto.Package.Update.Services;
using FunFair.Test.Common;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Credfeto.Package.Update.Tests.Services;

public sealed class LoggerProxyTests : TestBase
{
    private const string TEST_MESSAGE = "Test log message";

    private static Func<string, Exception?, string> SimpleFormatter => static (state, _) => state;

    [Fact]
    public void ConstructorWithNullLoggerThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new LoggerProxy<LoggerProxyTests>(null!));
    }

    [Fact]
    public void IsEnabledDelegatesToInnerLogger()
    {
        ILogger innerLogger = GetSubstitute<ILogger>();
        innerLogger.IsEnabled(LogLevel.Information).Returns(true);

        LoggerProxy<LoggerProxyTests> proxy = new(innerLogger);

        bool result = proxy.IsEnabled(LogLevel.Information);

        Assert.True(result, "IsEnabled should return the value from the inner logger");
        innerLogger.Received(1).IsEnabled(LogLevel.Information);
    }

    [Fact]
    public void BeginScopeDelegatesToInnerLogger()
    {
        ILogger innerLogger = GetSubstitute<ILogger>();
        IDisposable expectedScope = GetSubstitute<IDisposable>();
        innerLogger.BeginScope(Arg.Any<string>()).Returns(expectedScope);

        LoggerProxy<LoggerProxyTests> proxy = new(innerLogger);

        using IDisposable? result = proxy.BeginScope(state: "test scope");

        Assert.Same(expected: expectedScope, actual: result);
        _ = innerLogger.Received(1).BeginScope(Arg.Any<string>());
    }

    [Fact]
    public void LogDelegatesToInnerLogger()
    {
        ILogger innerLogger = GetSubstitute<ILogger>();

        LoggerProxy<LoggerProxyTests> proxy = new(innerLogger);

        proxy.Log(
            logLevel: LogLevel.Information,
            eventId: default,
            state: TEST_MESSAGE,
            exception: null,
            formatter: SimpleFormatter
        );

        innerLogger
            .Received(1)
            .Log(
                logLevel: LogLevel.Information,
                eventId: default,
                state: TEST_MESSAGE,
                exception: null,
                formatter: SimpleFormatter
            );
    }
}
