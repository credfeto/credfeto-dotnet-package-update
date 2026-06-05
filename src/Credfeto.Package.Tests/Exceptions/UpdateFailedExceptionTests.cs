using System;
using Credfeto.Package.Exceptions;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Package.Tests.Exceptions;

public sealed class UpdateFailedExceptionTests : TestBase
{
    private const string TEST_MESSAGE = "Test exception message";

    [Fact]
    public void DefaultConstructorCreatesException()
    {
        UpdateFailedException exception = new();

        Assert.NotNull(exception);
    }

    [Fact]
    public void DefaultConstructorSetsDefaultMessage()
    {
        UpdateFailedException exception = new();

        Assert.Equal(expected: "Update failed", actual: exception.Message);
    }

    [Fact]
    public void MessageConstructorSetsMessage()
    {
        UpdateFailedException exception = new(TEST_MESSAGE);

        Assert.Equal(expected: TEST_MESSAGE, actual: exception.Message);
    }

    [Fact]
    public void MessageAndInnerExceptionConstructorSetsMessage()
    {
        Exception innerException = new InvalidOperationException("Inner");
        UpdateFailedException exception = new(message: TEST_MESSAGE, innerException: innerException);

        Assert.Equal(expected: TEST_MESSAGE, actual: exception.Message);
    }

    [Fact]
    public void MessageAndInnerExceptionConstructorSetsInnerException()
    {
        Exception innerException = new InvalidOperationException("Inner");
        UpdateFailedException exception = new(message: TEST_MESSAGE, innerException: innerException);

        Assert.Same(expected: innerException, actual: exception.InnerException);
    }
}
