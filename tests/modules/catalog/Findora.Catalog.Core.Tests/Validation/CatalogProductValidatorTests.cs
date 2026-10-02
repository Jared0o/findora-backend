using System.Text.Json;
using Findora.Catalog.Core.Models;
using Findora.Catalog.Core.Validation;
using CatalogModel = Findora.Catalog.Core.Models.Catalog;

namespace Findora.Catalog.Core.Tests.Validation;

public sealed class CatalogProductValidatorTests
{
    [Theory]
    [InlineData(CatalogFieldType.Int, "0")]
    [InlineData(CatalogFieldType.Int, "-2147483648")]
    [InlineData(CatalogFieldType.Int, "2147483647")]
    [InlineData(CatalogFieldType.Decimal, "100")]
    [InlineData(CatalogFieldType.Decimal, "100.50")]
    [InlineData(CatalogFieldType.Decimal, "-0.25")]
    [InlineData(CatalogFieldType.String, "\"hello\"")]
    [InlineData(CatalogFieldType.String, "\"\"")]
    [InlineData(CatalogFieldType.Bool, "true")]
    [InlineData(CatalogFieldType.Bool, "false")]
    public void Validate_AcceptsMatchingScalar(CatalogFieldType type, string value)
    {
        var result = Validate("{\"value\":" + value + "}", new CatalogFieldDefinition("value", type));

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData(CatalogFieldType.Int, "2147483648", "ExpectedInt")]
    [InlineData(CatalogFieldType.Int, "-2147483649", "ExpectedInt")]
    [InlineData(CatalogFieldType.Int, "1.5", "ExpectedInt")]
    [InlineData(CatalogFieldType.Int, "\"123\"", "ExpectedInt")]
    [InlineData(CatalogFieldType.Decimal, "1e100", "ExpectedDecimal")]
    [InlineData(CatalogFieldType.Decimal, "\"100.50\"", "ExpectedDecimal")]
    [InlineData(CatalogFieldType.String, "123", "ExpectedString")]
    [InlineData(CatalogFieldType.Bool, "\"true\"", "ExpectedBool")]
    [InlineData(CatalogFieldType.Bool, "1", "ExpectedBool")]
    [InlineData(CatalogFieldType.String, "{}", "ExpectedString")]
    [InlineData(CatalogFieldType.String, "[]", "ExpectedString")]
    public void Validate_RejectsWrongScalarTypeOrRange(CatalogFieldType type, string value, string code)
    {
        var result = Validate("{\"value\":" + value + "}", new CatalogFieldDefinition("value", type));

        AssertError(result, "value", code);
    }

    [Theory]
    [InlineData(CatalogFieldType.Int, "[1,2,-3]")]
    [InlineData(CatalogFieldType.Decimal, "[1,2.50,-3.25]")]
    [InlineData(CatalogFieldType.String, "[\"a\",\"b\"]")]
    [InlineData(CatalogFieldType.Bool, "[true,false]")]
    [InlineData(CatalogFieldType.Int, "[]")]
    [InlineData(CatalogFieldType.Decimal, "[]")]
    [InlineData(CatalogFieldType.String, "[]")]
    [InlineData(CatalogFieldType.Bool, "[]")]
    public void Validate_AcceptsHomogeneousOrEmptyArray(CatalogFieldType type, string value)
    {
        var result = Validate("{\"value\":" + value + "}", new CatalogFieldDefinition("value", type, isArray: true));

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData(CatalogFieldType.Int, "[1,\"2\"]", "ExpectedInt")]
    [InlineData(CatalogFieldType.Decimal, "[1,true]", "ExpectedDecimal")]
    [InlineData(CatalogFieldType.String, "[\"a\",2]", "ExpectedString")]
    [InlineData(CatalogFieldType.Bool, "[true,1]", "ExpectedBool")]
    [InlineData(CatalogFieldType.String, "[\"a\",[\"b\"]]", "ExpectedString")]
    [InlineData(CatalogFieldType.String, "[\"a\",{}]", "ExpectedString")]
    [InlineData(CatalogFieldType.String, "[\"a\",null]", "NullNotAllowed")]
    public void Validate_ReportsInvalidArrayElementWithIndex(CatalogFieldType type, string value, string code)
    {
        var result = Validate("{\"value\":" + value + "}", new CatalogFieldDefinition("value", type, isArray: true));

        AssertError(result, "value[1]", code);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("\"text\"")]
    [InlineData("true")]
    [InlineData("{}")]
    [InlineData("null")]
    public void Validate_RejectsNonArrayForArrayField(string value)
    {
        var result = Validate("{\"value\":" + value + "}", new CatalogFieldDefinition("value", CatalogFieldType.String, isArray: true));

        AssertError(result, "value", "ExpectedArray");
    }

    [Theory]
    [InlineData(CatalogFieldType.Int)]
    [InlineData(CatalogFieldType.Decimal)]
    [InlineData(CatalogFieldType.String)]
    [InlineData(CatalogFieldType.Bool)]
    public void Validate_RejectsNullScalar(CatalogFieldType type)
    {
        AssertError(Validate("{\"value\":null}", new CatalogFieldDefinition("value", type)), "value", "NullNotAllowed");
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("1")]
    [InlineData("\"text\"")]
    [InlineData("true")]
    public void Validate_RequiresObjectDocument(string json)
    {
        AssertError(Validate(json), "$", "ExpectedObject");
    }

    [Fact]
    public void Validate_AllowsMissingOptionalFields()
    {
        Assert.True(Validate("{}", new CatalogFieldDefinition("value", CatalogFieldType.String)).IsValid);
    }

    [Fact]
    public void Validate_ReportsMissingRequiredField()
    {
        AssertError(Validate("{}", new CatalogFieldDefinition("value", CatalogFieldType.String, isRequired: true)),
            "value", "RequiredField");
    }

    [Fact]
    public void Validate_AllowsEmptyRequiredArray()
    {
        Assert.True(Validate("{\"value\":[]}",
            new CatalogFieldDefinition("value", CatalogFieldType.String, isArray: true, isRequired: true)).IsValid);
    }

    [Fact]
    public void Validate_RejectsUnknownFieldWithoutRegisteringIt()
    {
        var catalog = new CatalogModel(Guid.NewGuid(), "Products");
        using var document = JsonDocument.Parse("{\"unknown\":1}");

        var result = CatalogProductValidator.Validate(document.RootElement, catalog);

        AssertError(result, "unknown", "UnknownField");
        Assert.Empty(catalog.Fields);
    }

    [Fact]
    public void Validate_UsesCaseSensitiveFieldNames()
    {
        AssertError(Validate("{\"Name\":\"shoe\"}", new CatalogFieldDefinition("name", CatalogFieldType.String)),
            "Name", "UnknownField");
    }

    [Fact]
    public void Validate_RejectsDuplicateProperties()
    {
        AssertError(Validate("{\"value\":\"first\",\"value\":\"second\"}",
            new CatalogFieldDefinition("value", CatalogFieldType.String)), "value", "DuplicateField");
    }

    [Fact]
    public void Validate_CollectsErrorsAcrossFieldsAndArrayElements()
    {
        var result = Validate("{\"name\":null,\"price\":\"bad\",\"tags\":[1,2]}",
            new CatalogFieldDefinition("name", CatalogFieldType.String),
            new CatalogFieldDefinition("price", CatalogFieldType.Decimal),
            new CatalogFieldDefinition("tags", CatalogFieldType.String, isArray: true),
            new CatalogFieldDefinition("required", CatalogFieldType.Bool, isRequired: true));

        Assert.False(result.IsValid);
        Assert.Collection(result.Errors,
            error => Assert.Equal(("name", "NullNotAllowed"), (error.Path, error.Code)),
            error => Assert.Equal(("price", "ExpectedDecimal"), (error.Path, error.Code)),
            error => Assert.Equal(("tags[0]", "ExpectedString"), (error.Path, error.Code)),
            error => Assert.Equal(("tags[1]", "ExpectedString"), (error.Path, error.Code)),
            error => Assert.Equal(("required", "RequiredField"), (error.Path, error.Code)));
    }

    [Fact]
    public void Validate_UsesDefinitionsFromSelectedCatalog()
    {
        var first = Validate("{\"size\":43}", new CatalogFieldDefinition("size", CatalogFieldType.Int));
        var second = Validate("{\"size\":\"large\"}", new CatalogFieldDefinition("size", CatalogFieldType.String));

        Assert.True(first.IsValid);
        Assert.True(second.IsValid);
    }

    [Fact]
    public void Validate_RejectsNullCatalog()
    {
        using var document = JsonDocument.Parse("{}");

        Assert.Throws<ArgumentNullException>(() => CatalogProductValidator.Validate(document.RootElement, null!));
    }

    private static CatalogValidationResult Validate(string json, params CatalogFieldDefinition[] definitions)
    {
        var catalog = new CatalogModel(Guid.NewGuid(), "Products");
        foreach (var definition in definitions)
        {
            catalog.AddField(definition);
        }

        using var document = JsonDocument.Parse(json);
        return CatalogProductValidator.Validate(document.RootElement, catalog);
    }

    private static void AssertError(CatalogValidationResult result, string path, string code)
    {
        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(path, error.Path);
        Assert.Equal(code, error.Code);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
    }
}

