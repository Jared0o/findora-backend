using Findora.Shared.Abstraction.Results;

namespace Findora.Shared.Abstraction.Tests.Results;

public sealed class ErrorTests
{
    [Fact]
    public void Constructor_AllowsErrorWithoutFieldPath()
    {
        Assert.Null(new Error("Catalog.NotFound", "Missing catalog.").Path);
    }

    [Fact]
    public void Errors_WithDifferentPathsAreNotEqual()
    {
        Assert.NotEqual(new Error("InvalidValue", "Invalid value.", "name"),
            new Error("InvalidValue", "Invalid value.", "fields[1]"));
    }

    [Theory]
    [InlineData(null, "Message")]
    [InlineData("", "Message")]
    [InlineData(" ", "Message")]
    [InlineData("Code", null)]
    [InlineData("Code", "")]
    [InlineData("Code", " ")]
    public void Constructor_RejectsMissingCodeOrMessage(string? code, string? message)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Error(code!, message!));
    }

    [Fact]
    public void Errors_WithSameCodeAndMessageAreEqual()
    {
        Assert.Equal(new Error("Catalog.NotFound", "Missing catalog."),
            new Error("Catalog.NotFound", "Missing catalog."));
    }
}
