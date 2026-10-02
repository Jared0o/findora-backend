using Findora.Catalog.Core.Commands.CreateCatalog;

namespace Findora.Catalog.Core.Tests.Commands.CreateCatalog;

public sealed class CreateCatalogCommandValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public async Task ValidateAsync_RejectsMissingName(string? name)
    {
        var validator = new CreateCatalogCommandValidator();

        var result = await validator.ValidateAsync(new CreateCatalogCommand(name!),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal("Catalog.NameRequired", error.ErrorCode);
        Assert.Equal("Catalog name is required.", error.ErrorMessage);
        Assert.Equal("name", error.PropertyName);
    }

    [Theory]
    [InlineData("Products")]
    [InlineData("  Products  ")]
    [InlineData("Product catalog")]
    public async Task ValidateAsync_AcceptsNonEmptyName(string name)
    {
        var validator = new CreateCatalogCommandValidator();

        var result = await validator.ValidateAsync(new CreateCatalogCommand(name),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
}
