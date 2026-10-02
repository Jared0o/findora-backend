using Findora.Shared.Abstraction.Results;

namespace Findora.Shared.Abstraction.Tests.Results;

public sealed class ResultCollectionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Failure_PreservesMultipleErrorsAndTheirOrder(bool generic)
    {
        Error[] errors = [
            new("Catalog.NameRequired", "Name is required.", "name"),
            new("Catalog.InvalidField", "Invalid field type.", "fields[1]")
        ];

        var actual = GetErrors(errors, generic);

        Assert.Equal(errors, actual);
        Assert.Equal("name", actual[0].Path);
        Assert.Equal("fields[1]", actual[1].Path);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Failure_RejectsEmptyCollection(bool generic)
    {
        Assert.Throws<ArgumentException>(() => GetErrors([], generic));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Failure_RejectsNullCollection(bool generic)
    {
        Assert.Throws<ArgumentNullException>(() => GetErrors(null!, generic));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Failure_RejectsNullElements(bool generic)
    {
        Assert.Throws<ArgumentException>(() => GetErrors([
            new Error("Catalog.Invalid", "Invalid catalog."), null!
        ], generic));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Failure_IsUnaffectedByChangesToOriginalCollection(bool generic)
    {
        var original = new Error("Catalog.NameRequired", "Name is required.", "name");
        var errors = new List<Error> { original };
        var actual = GetErrors(errors, generic);

        errors[0] = new Error("Catalog.Other", "Other error.");
        errors.Clear();

        Assert.Same(original, Assert.Single(actual));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Failure_ErrorsCannotBeChangedThroughPublicCollection(bool generic)
    {
        var actual = GetErrors([new Error("Catalog.Invalid", "Invalid catalog.")], generic);
        var list = Assert.IsAssignableFrom<IList<Error>>(actual);

        Assert.Throws<NotSupportedException>(() => list.Clear());
        Assert.Single(actual);
    }

    [Fact]
    public void GenericFailure_WithMultipleErrorsHasNoValue()
    {
        var result = Result<Guid>.Failure([
            new Error("Catalog.NameRequired", "Name is required.", "name"),
            new Error("Catalog.InvalidField", "Invalid field.", "fields[1]")
        ]);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    private static IReadOnlyList<Error> GetErrors(IEnumerable<Error> errors, bool generic)
    {
        if (generic)
        {
            var result = Result<Guid>.Failure(errors);
            Assert.True(result.IsFailure);
            Assert.False(result.IsSuccess);
            return result.Errors;
        }

        var nonGeneric = Result.Failure(errors);
        Assert.True(nonGeneric.IsFailure);
        Assert.False(nonGeneric.IsSuccess);
        return nonGeneric.Errors;
    }
}
