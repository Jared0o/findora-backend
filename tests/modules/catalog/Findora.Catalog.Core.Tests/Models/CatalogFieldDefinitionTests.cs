using Findora.Catalog.Core.Models;

namespace Findora.Catalog.Core.Tests.Models;

public sealed class CatalogFieldDefinitionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_RejectsMissingName(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => new CatalogFieldDefinition(name!, CatalogFieldType.String));
    }

    [Fact]
    public void Constructor_RejectsUnsupportedType()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CatalogFieldDefinition("name", (CatalogFieldType)999));
    }
}
