using System.Diagnostics;
using FunFair.Test.Common;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Credfeto.Package.Update.Tests;

public sealed class EnumExtensionsTests : TestBase
{
    [Theory]
    [InlineData(LogLevel.Trace, "Trace")]
    [InlineData(LogLevel.Debug, "Debug")]
    [InlineData(LogLevel.Information, "Information")]
    [InlineData(LogLevel.Warning, "Warning")]
    [InlineData(LogLevel.Error, "Error")]
    [InlineData(LogLevel.Critical, "Critical")]
    [InlineData(LogLevel.None, "None")]
    public static void LogLevelGetNameReturnsExpectedString(LogLevel logLevel, string expected)
    {
        string result = logLevel.GetName();

        Assert.Equal(expected: expected, actual: result);
    }

    [Theory]
    [InlineData(LogLevel.Trace)]
    [InlineData(LogLevel.Debug)]
    [InlineData(LogLevel.Information)]
    [InlineData(LogLevel.Warning)]
    [InlineData(LogLevel.Error)]
    [InlineData(LogLevel.Critical)]
    [InlineData(LogLevel.None)]
    public static void LogLevelGetDescriptionReturnsNonEmptyString(LogLevel value)
    {
        string description = value.GetDescription();

        Assert.NotEmpty(description);
    }

    [Theory]
    [InlineData(LogLevel.Trace)]
    [InlineData(LogLevel.Debug)]
    [InlineData(LogLevel.Information)]
    [InlineData(LogLevel.Warning)]
    [InlineData(LogLevel.Error)]
    [InlineData(LogLevel.Critical)]
    [InlineData(LogLevel.None)]
    public static void LogLevelIsDefinedReturnsTrueForKnownValues(LogLevel value)
    {
        bool defined = value.IsDefined();

        Assert.True(defined, userMessage: "Expected IsDefined to return true for a known LogLevel value");
    }

    [Fact]
    public void LogLevelIsDefinedReturnsFalseForUnknownValue()
    {
        bool defined = ((LogLevel)int.MaxValue).IsDefined();

        Assert.False(defined, userMessage: "Expected IsDefined to return false for an unknown LogLevel value");
    }

    [Fact]
    public void LogLevelGetNameThrowsForUnknownValue()
    {
        Assert.Throws<UnreachableException>(() => ((LogLevel)int.MaxValue).GetName());
    }
}
