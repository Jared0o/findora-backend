using Findora.Shared.Abstraction.Results;

namespace Findora.Shared.Abstraction.Tests.Results;

public sealed class ResultTests
{
    [Fact]
    public void Success_HasNoError()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Failure_PreservesError()
    {
        var error = new Error("Catalog.NotFound", "The catalog does not exist.");
        var result = Result.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Same(error, Assert.Single(result.Errors));
    }

    [Fact]
    public void Failure_RejectsNullError()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Failure((Error)null!));
    }
}
