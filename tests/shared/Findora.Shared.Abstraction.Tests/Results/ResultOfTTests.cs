using Findora.Shared.Abstraction.Results;

namespace Findora.Shared.Abstraction.Tests.Results;

public sealed class ResultOfTTests
{
    [Fact]
    public void Success_PreservesReferenceValueAndHasNoError()
    {
        var value = new object();
        var result = Result<object>.Success(value);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Empty(result.Errors);
        Assert.Same(value, result.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    public void Success_AllowsValueTypesIncludingDefault(int value)
    {
        var result = Result<int>.Success(value);

        Assert.True(result.IsSuccess);
        Assert.Equal(value, result.Value);
    }

    [Fact]
    public void Success_RejectsNullValue()
    {
        Assert.Throws<ArgumentNullException>(() => Result<string>.Success(null!));
    }

    [Fact]
    public void Failure_PreservesErrorAndRejectsValueAccess()
    {
        var error = new Error("Catalog.NotFound", "The catalog does not exist.");
        var result = Result<string>.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Same(error, Assert.Single(result.Errors));
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Failure_DoesNotExposeDefaultValueForValueType()
    {
        var result = Result<int>.Failure(new Error("Catalog.NotFound", "The catalog does not exist."));

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Failure_RejectsNullError()
    {
        Assert.Throws<ArgumentNullException>(() => Result<string>.Failure((Error)null!));
    }
}
