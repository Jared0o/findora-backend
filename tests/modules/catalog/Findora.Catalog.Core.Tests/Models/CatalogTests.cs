using Findora.Catalog.Core.Models;
using CatalogModel = Findora.Catalog.Core.Models.Catalog;

namespace Findora.Catalog.Core.Tests.Models;

public sealed class CatalogTests
{
    [Fact]
    public void Constructor_RejectsEmptyId()
    {
        Assert.Throws<ArgumentException>(() => new CatalogModel(Guid.Empty, "Products"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_RejectsMissingName(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => new CatalogModel(Guid.NewGuid(), name!));
    }

    [Fact]
    public void AddField_MakesDefinitionAvailable()
    {
        var catalog = new CatalogModel(Guid.NewGuid(), "Products");
        var definition = new CatalogFieldDefinition("price", CatalogFieldType.Decimal);

        catalog.AddField(definition);

        Assert.Same(definition, catalog.Fields["price"]);
    }

    [Fact]
    public void AddField_RejectsDuplicateNameWithoutReplacingDefinition()
    {
        var catalog = new CatalogModel(Guid.NewGuid(), "Products");
        var original = new CatalogFieldDefinition("price", CatalogFieldType.Decimal);
        catalog.AddField(original);

        Assert.Throws<InvalidOperationException>(() =>
            catalog.AddField(new CatalogFieldDefinition("price", CatalogFieldType.String)));

        Assert.Same(original, catalog.Fields["price"]);
        Assert.Single(catalog.Fields);
    }

    [Fact]
    public void AddField_RejectsNull()
    {
        var catalog = new CatalogModel(Guid.NewGuid(), "Products");

        Assert.Throws<ArgumentNullException>(() => catalog.AddField(null!));
    }

    [Fact]
    public void Fields_CannotBeChangedThroughPublicCollection()
    {
        var catalog = new CatalogModel(Guid.NewGuid(), "Products");
        var fields = Assert.IsAssignableFrom<IDictionary<string, CatalogFieldDefinition>>(catalog.Fields);

        Assert.Throws<NotSupportedException>(() =>
            fields.Add("price", new CatalogFieldDefinition("price", CatalogFieldType.Decimal)));
        Assert.Empty(catalog.Fields);
    }

    [Fact]
    public void Catalogs_AllowDifferentTypesForSameFieldName()
    {
        var first = new CatalogModel(Guid.NewGuid(), "First");
        var second = new CatalogModel(Guid.NewGuid(), "Second");
        first.AddField(new CatalogFieldDefinition("size", CatalogFieldType.Int));
        second.AddField(new CatalogFieldDefinition("size", CatalogFieldType.String));

        Assert.Equal(CatalogFieldType.Int, first.Fields["size"].Type);
        Assert.Equal(CatalogFieldType.String, second.Fields["size"].Type);
    }
}
