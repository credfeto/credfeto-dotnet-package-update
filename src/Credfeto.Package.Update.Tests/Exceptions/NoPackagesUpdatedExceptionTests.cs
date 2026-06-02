using System;
using Credfeto.Package.Update.Exceptions;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Package.Update.Tests.Exceptions;

public sealed class NoPackagesUpdatedExceptionTests : TestBase
{
    private const string TEST_MESSAGE = "Test exception message";

    [Fact]
    public void DefaultConstructorCreatesException()
    {
        NoPackagesUpdatedException exception = new();

        Assert.NotNull(exception);
    }

    [Fact]
    public void MessageConstructorSetsMessage()
    {
        NoPackagesUpdatedException exception = new(TEST_MESSAGE);

        Assert.Equal(expected: TEST_MESSAGE, actual: exception.Message);
    }

    [Fact]
    public void MessageAndInnerExceptionConstructorSetsMessageAndInnerException()
    {
        NoPackagesUpdatedException innerException = new("Inner exception");
        NoPackagesUpdatedException exception = new(message: TEST_MESSAGE, innerException: innerException);

        Assert.Equal(expected: TEST_MESSAGE, actual: exception.Message);
        Assert.Same(expected: innerException, actual: exception.InnerException);
    }
}
