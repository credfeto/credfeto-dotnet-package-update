using System.Diagnostics;
using CommandLine;
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
    public void LogLevelGetNameReturnsExpectedString(LogLevel logLevel, string expected)
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
    public void LogLevelGetDescriptionReturnsNonEmptyString(LogLevel value)
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
    public void LogLevelIsDefinedReturnsTrueForKnownValues(LogLevel value)
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

    [Theory]
    [InlineData(ErrorType.BadFormatTokenError, "BadFormatTokenError")]
    [InlineData(ErrorType.MissingValueOptionError, "MissingValueOptionError")]
    [InlineData(ErrorType.UnknownOptionError, "UnknownOptionError")]
    [InlineData(ErrorType.MissingRequiredOptionError, "MissingRequiredOptionError")]
    [InlineData(ErrorType.MutuallyExclusiveSetError, "MutuallyExclusiveSetError")]
    [InlineData(ErrorType.BadFormatConversionError, "BadFormatConversionError")]
    [InlineData(ErrorType.SequenceOutOfRangeError, "SequenceOutOfRangeError")]
    [InlineData(ErrorType.RepeatedOptionError, "RepeatedOptionError")]
    [InlineData(ErrorType.NoVerbSelectedError, "NoVerbSelectedError")]
    [InlineData(ErrorType.BadVerbSelectedError, "BadVerbSelectedError")]
    [InlineData(ErrorType.HelpRequestedError, "HelpRequestedError")]
    [InlineData(ErrorType.HelpVerbRequestedError, "HelpVerbRequestedError")]
    [InlineData(ErrorType.VersionRequestedError, "VersionRequestedError")]
    [InlineData(ErrorType.SetValueExceptionError, "SetValueExceptionError")]
    [InlineData(ErrorType.InvalidAttributeConfigurationError, "InvalidAttributeConfigurationError")]
    [InlineData(ErrorType.MissingGroupOptionError, "MissingGroupOptionError")]
    [InlineData(ErrorType.GroupOptionAmbiguityError, "GroupOptionAmbiguityError")]
    [InlineData(ErrorType.MultipleDefaultVerbsError, "MultipleDefaultVerbsError")]
    public void ErrorTypeGetNameReturnsExpectedString(ErrorType errorType, string expected)
    {
        string result = errorType.GetName();

        Assert.Equal(expected: expected, actual: result);
    }

    [Theory]
    [InlineData(ErrorType.BadFormatTokenError)]
    [InlineData(ErrorType.MissingValueOptionError)]
    [InlineData(ErrorType.UnknownOptionError)]
    [InlineData(ErrorType.MissingRequiredOptionError)]
    [InlineData(ErrorType.MutuallyExclusiveSetError)]
    [InlineData(ErrorType.BadFormatConversionError)]
    [InlineData(ErrorType.SequenceOutOfRangeError)]
    [InlineData(ErrorType.RepeatedOptionError)]
    [InlineData(ErrorType.NoVerbSelectedError)]
    [InlineData(ErrorType.BadVerbSelectedError)]
    [InlineData(ErrorType.HelpRequestedError)]
    [InlineData(ErrorType.HelpVerbRequestedError)]
    [InlineData(ErrorType.VersionRequestedError)]
    [InlineData(ErrorType.SetValueExceptionError)]
    [InlineData(ErrorType.InvalidAttributeConfigurationError)]
    [InlineData(ErrorType.MissingGroupOptionError)]
    [InlineData(ErrorType.GroupOptionAmbiguityError)]
    [InlineData(ErrorType.MultipleDefaultVerbsError)]
    public void ErrorTypeGetDescriptionReturnsNonEmptyString(ErrorType value)
    {
        string description = value.GetDescription();

        Assert.NotEmpty(description);
    }

    [Theory]
    [InlineData(ErrorType.BadFormatTokenError)]
    [InlineData(ErrorType.MissingValueOptionError)]
    [InlineData(ErrorType.UnknownOptionError)]
    [InlineData(ErrorType.MissingRequiredOptionError)]
    [InlineData(ErrorType.MutuallyExclusiveSetError)]
    [InlineData(ErrorType.BadFormatConversionError)]
    [InlineData(ErrorType.SequenceOutOfRangeError)]
    [InlineData(ErrorType.RepeatedOptionError)]
    [InlineData(ErrorType.NoVerbSelectedError)]
    [InlineData(ErrorType.BadVerbSelectedError)]
    [InlineData(ErrorType.HelpRequestedError)]
    [InlineData(ErrorType.HelpVerbRequestedError)]
    [InlineData(ErrorType.VersionRequestedError)]
    [InlineData(ErrorType.SetValueExceptionError)]
    [InlineData(ErrorType.InvalidAttributeConfigurationError)]
    [InlineData(ErrorType.MissingGroupOptionError)]
    [InlineData(ErrorType.GroupOptionAmbiguityError)]
    [InlineData(ErrorType.MultipleDefaultVerbsError)]
    public void ErrorTypeIsDefinedReturnsTrueForKnownValues(ErrorType value)
    {
        bool defined = value.IsDefined();

        Assert.True(defined, userMessage: "Expected IsDefined to return true for a known ErrorType value");
    }

    [Fact]
    public void ErrorTypeIsDefinedReturnsFalseForUnknownValue()
    {
        bool defined = ((ErrorType)int.MaxValue).IsDefined();

        Assert.False(defined, userMessage: "Expected IsDefined to return false for an unknown ErrorType value");
    }

    [Fact]
    public void ErrorTypeGetNameThrowsForUnknownValue()
    {
        Assert.Throws<UnreachableException>(() => ((ErrorType)int.MaxValue).GetName());
    }
}
