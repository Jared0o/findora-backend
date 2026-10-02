using System.Text.Json;
using Findora.Catalog.Core.Models;
using Findora.Catalog.Core.Validation;
using CatalogModel = Findora.Catalog.Core.Models.Catalog;

namespace Findora.Catalog.Core.Tests.Validation;

public sealed class CatalogDocumentAnalyzerTests
{
    [Theory]
    [InlineData("1", CatalogFieldType.Int, false)]
    [InlineData("2147483648", CatalogFieldType.Decimal, false)]
    [InlineData("10.0", CatalogFieldType.Decimal, false)]
    [InlineData("1e2", CatalogFieldType.Decimal, false)]
    [InlineData("2.5", CatalogFieldType.Decimal, false)]
    [InlineData("\"hello\"", CatalogFieldType.String, false)]
    [InlineData("true", CatalogFieldType.Bool, false)]
    [InlineData("[1,2]", CatalogFieldType.Int, true)]
    [InlineData("[1,2.5]", CatalogFieldType.Decimal, true)]
    [InlineData("[2.5,1]", CatalogFieldType.Decimal, true)]
    [InlineData("[\"a\",\"b\"]", CatalogFieldType.String, true)]
    [InlineData("[true,false]", CatalogFieldType.Bool, true)]
    public void Analyze_InfersOptionalFieldWithoutMutatingCatalog(string value, CatalogFieldType type, bool isArray)
    {
        using var document = JsonDocument.Parse("{\"field\":" + value + "}");
        var catalog = new CatalogModel(Guid.NewGuid(), "Test");
        var result = CatalogDocumentAnalyzer.Analyze(document.RootElement, catalog);
        Assert.True(result.IsSuccess);
        var field = Assert.Single(result.Value);
        Assert.Equal("field", field.Name);
        Assert.Equal(type, field.Type);
        Assert.Equal(isArray, field.IsArray);
        Assert.False(field.IsRequired);
        Assert.Empty(catalog.Fields);
    }

    [Theory]
    [InlineData("{}", "EmptyDocument", "$")]
    [InlineData("[]", "ExpectedObject", "$")]
    [InlineData("null", "ExpectedObject", "$")]
    [InlineData("{\"x\":[]}", "UnknownArrayType", "x")]
    [InlineData("{\"x\":null}", "NullNotAllowed", "x")]
    [InlineData("{\"x\":{}}", "UnsupportedType", "x")]
    [InlineData("{\"x\":[[]]}", "UnsupportedType", "x[0]")]
    [InlineData("{\"x\":[1,\"a\"]}", "MixedArrayTypes", "x[1]")]
    [InlineData("{\"x\":[null]}", "NullNotAllowed", "x[0]")]
    [InlineData("{\"x\":1,\"x\":2}", "DuplicateField", "x")]
    [InlineData("{\" \":1}", "InvalidFieldName", " ")]
    [InlineData("{\"x\":1e1000}", "NumberOutOfRange", "x")]
    [InlineData("{\"x\":\"\u0000\"}", "ExpectedString", "x")]
    public void Analyze_RejectsInvalidDocument(string json, string code, string path)
    {
        // Escape the null character so it remains valid JSON before semantic validation.
        using var document = JsonDocument.Parse(json.Replace("\0", "\\u0000", StringComparison.Ordinal));
        var catalog = new CatalogModel(Guid.NewGuid(), "Test");
        var result = CatalogDocumentAnalyzer.Analyze(document.RootElement, catalog);
        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == code && error.Path == path);
        Assert.Empty(catalog.Fields);
    }

    [Fact]
    public void Analyze_AcceptsExistingDecimalAndEmptyTypedArray()
    {
        var catalog = new CatalogModel(Guid.NewGuid(), "Test");
        catalog.AddField(new("price", CatalogFieldType.Decimal));
        catalog.AddField(new("tags", CatalogFieldType.String, isArray: true));
        catalog.AddField(new("optional", CatalogFieldType.Bool));
        using var document = JsonDocument.Parse("{\"price\":10,\"tags\":[],\"new\":true}");
        var result = CatalogDocumentAnalyzer.Analyze(document.RootElement, catalog);
        Assert.True(result.IsSuccess);
        Assert.Equal("new", Assert.Single(result.Value).Name);
        Assert.Equal(3, catalog.Fields.Count);
    }

    [Fact]
    public void Analyze_CollectsErrorsWithoutChangingKnownTypesOrAddingFields()
    {
        var catalog = new CatalogModel(Guid.NewGuid(), "Test");
        catalog.AddField(new("count", CatalogFieldType.Int));
        catalog.AddField(new("required", CatalogFieldType.Bool, isRequired: true));
        using var document = JsonDocument.Parse("{\"count\":1.5,\"fresh\":true,\"bad\":null}");
        var result = CatalogDocumentAnalyzer.Analyze(document.RootElement, catalog);
        Assert.True(result.IsFailure);
        Assert.Equal(3, result.Errors.Count);
        Assert.Contains(result.Errors, error => error.Code == "RequiredField");
        Assert.Contains(result.Errors, error => error.Code == "ExpectedInt");
        Assert.Contains(result.Errors, error => error.Code == "NullNotAllowed");
        Assert.Equal(2, catalog.Fields.Count);
    }
}
