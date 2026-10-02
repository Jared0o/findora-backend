using System.Text.Json;
using Findora.Catalog.Core.Models;
using Findora.Catalog.Core.Validation;
using CatalogModel = Findora.Catalog.Core.Models.Catalog;

namespace Findora.Catalog.Core.Tests.Validation;

public sealed class CatalogDocumentBatchAnalyzerTests
{
    [Fact]
    public void Analyze_ReusesEarlierDefinitionsWithoutMutatingOriginalCatalog()
    {
        using var json = JsonDocument.Parse("[{\"score\":10.0,\"tags\":[\"a\"]},{\"score\":20,\"tags\":[],\"published\":true}]");
        var catalog = new CatalogModel(Guid.NewGuid(), "Test");
        var result = CatalogDocumentBatchAnalyzer.Analyze(json.RootElement.EnumerateArray().ToArray(), catalog);
        Assert.True(result.IsSuccess);
        Assert.Equal(["score", "tags", "published"], result.Value.Select(field => field.Name));
        Assert.Equal(CatalogFieldType.Decimal, result.Value[0].Type);
        Assert.True(result.Value[1].IsArray);
        Assert.All(result.Value, field => Assert.False(field.IsRequired));
        Assert.Empty(catalog.Fields);
    }

    [Fact]
    public void Analyze_CollectsIndexedErrorsFromAllDocuments()
    {
        using var json = JsonDocument.Parse("[{\"score\":1},{\"score\":1.5},{},null,{\"tags\":[1,\"wrong\"]},{\"score\":\"bad\"}]");
        var catalog = new CatalogModel(Guid.NewGuid(), "Test");
        var result = CatalogDocumentBatchAnalyzer.Analyze(json.RootElement.EnumerateArray().ToArray(), catalog);
        Assert.True(result.IsFailure);
        Assert.Equal(["documents[1].score", "documents[2]", "documents[3]", "documents[4].tags[1]", "documents[5].score"],
            result.Errors.Select(error => error.Path));
        Assert.Empty(catalog.Fields);
    }

    [Fact]
    public void Analyze_DoesNotChangeExistingFieldTypes()
    {
        using var json = JsonDocument.Parse("[{\"count\":1.0}]");
        var catalog = new CatalogModel(Guid.NewGuid(), "Test");
        catalog.AddField(new("count", CatalogFieldType.Int));
        var result = CatalogDocumentBatchAnalyzer.Analyze(json.RootElement.EnumerateArray().ToArray(), catalog);
        Assert.True(result.IsFailure);
        Assert.Equal("ExpectedInt", Assert.Single(result.Errors).Code);
        Assert.Equal(CatalogFieldType.Int, catalog.Fields["count"].Type);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Analyze_EnforcesBatchSize(int count, bool valid)
    {
        using var json = JsonDocument.Parse("{\"x\":1}");
        var result = CatalogDocumentBatchAnalyzer.Analyze(Enumerable.Repeat(json.RootElement, count).ToArray(), new CatalogModel(Guid.NewGuid(), "Test"));
        Assert.Equal(valid, result.IsSuccess);
    }
}
