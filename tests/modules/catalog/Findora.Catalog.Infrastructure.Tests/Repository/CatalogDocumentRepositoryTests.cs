using System.Text.Json;
using Findora.Catalog.Core.Models;
using Findora.Catalog.Infrastructure.Persistence;
using Findora.Catalog.Infrastructure.Persistence.Entities;
using Findora.Catalog.Infrastructure.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Findora.Catalog.Infrastructure.Tests.Repository;

[Collection("PostgreSQL")]
public sealed class CatalogDocumentRepositoryTests(PostgresFixture database)
{
    [Fact]
    public async Task Create_PersistsJsonbAndOptionalFieldsThenReusesSchema()
    {
        var token = TestContext.Current.CancellationToken;
        var id = await CreateCatalogAsync();
        using var document = JsonDocument.Parse("{\"title\":\"Article\",\"price\":10.5,\"tags\":[\"news\"],\"active\":true}");
        Guid documentId;
        await using (var writer = database.CreateContext())
        {
            var repository = new CatalogDocumentRepository(writer);
            var result = await repository.CreateAsync(id, document.RootElement, token);
            Assert.True(result.IsSuccess);
            documentId = result.Value;
            using var next = JsonDocument.Parse("{\"price\":10,\"tags\":[],\"views\":1}");
            Assert.True((await repository.CreateAsync(id, next.RootElement, token)).IsSuccess);
        }

        await using var reader = database.CreateContext();
        var saved = await reader.Documents.SingleAsync(record => record.Id == documentId, token);
        Assert.Equal(id, saved.CatalogId);
        Assert.Equal(7, saved.Id.Version);
        Assert.Equal(TimeSpan.Zero, saved.CreatedAt.Offset);
        using var data = JsonDocument.Parse(saved.Data);
        Assert.Equal("Article", data.RootElement.GetProperty("title").GetString());
        Assert.Equal(10.5m, data.RootElement.GetProperty("price").GetDecimal());
        var fields = await reader.FieldDefinitions.Where(field => field.CatalogId == id).ToListAsync(token);
        Assert.Equal(5, fields.Count);
        Assert.All(fields, field => Assert.False(field.IsRequired));
        Assert.Equal(CatalogFieldType.Decimal, fields.Single(field => field.Name == "price").Type);
        Assert.Equal(2, await reader.Documents.CountAsync(record => record.CatalogId == id, token));
        Assert.Equal("jsonb", reader.Model.FindEntityType(typeof(DocumentRecord))!.FindProperty(nameof(DocumentRecord.Data))!.GetColumnType());
    }

    [Fact]
    public async Task InvalidDocument_DoesNotPersistDocumentOrNewFields()
    {
        var token = TestContext.Current.CancellationToken;
        var id = await CreateCatalogAsync();
        await using var context = database.CreateContext();
        var repository = new CatalogDocumentRepository(context);
        using var first = JsonDocument.Parse("{\"count\":1}");
        Assert.True((await repository.CreateAsync(id, first.RootElement, token)).IsSuccess);
        using var invalid = JsonDocument.Parse("{\"count\":\"wrong\",\"newField\":true}");
        var result = await repository.CreateAsync(id, invalid.RootElement, token);
        Assert.True(result.IsFailure);
        Assert.Equal("ExpectedInt", Assert.Single(result.Errors).Code);
        Assert.Empty(context.ChangeTracker.Entries());
        Assert.Equal(1, await context.Documents.CountAsync(record => record.CatalogId == id, token));
        Assert.Equal("count", Assert.Single(await context.FieldDefinitions.Where(field => field.CatalogId == id).ToListAsync(token)).Name);
    }

    [Fact]
    public async Task SaveFailure_RollsBackDocumentAndFieldsAndReleasesLock()
    {
        var id = await CreateCatalogAsync();
        var token = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(database.ConnectionString)
            .AddInterceptors(new FailAfterSave()).Options;
        using var document = JsonDocument.Parse("{\"newField\":1}");
        await using (var context = new CatalogDbContext(options))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => new CatalogDocumentRepository(context).CreateAsync(id, document.RootElement, token));
            Assert.Empty(context.ChangeTracker.Entries());
        }
        await using var reader = database.CreateContext();
        Assert.False(await reader.Documents.AnyAsync(record => record.CatalogId == id, token));
        Assert.False(await reader.FieldDefinitions.AnyAsync(field => field.CatalogId == id, token));
        Assert.True((await new CatalogDocumentRepository(reader).CreateAsync(id, document.RootElement, token)).IsSuccess);
    }

    [Theory]
    [InlineData("1", "2", 2)]
    [InlineData("1", "\"text\"", 1)]
    public async Task ConcurrentDocuments_AgreeOnOneFieldType(string firstValue, string secondValue, int expectedSuccesses)
    {
        var id = await CreateCatalogAsync();
        var token = TestContext.Current.CancellationToken;
        using var first = JsonDocument.Parse("{\"field\":" + firstValue + "}");
        using var second = JsonDocument.Parse("{\"field\":" + secondValue + "}");
        await using var firstContext = database.CreateContext();
        await using var secondContext = database.CreateContext();
        // Hold the lock until both writers have begun their requests.
        await using var blocker = database.CreateContext();
        await using var transaction = await blocker.Database.BeginTransactionAsync(token);
        await blocker.Database.SqlQuery<Guid>($"SELECT id AS \"Value\" FROM catalog.catalogs WHERE id = {id} FOR UPDATE").ToListAsync(token);
        var firstTask = new CatalogDocumentRepository(firstContext).CreateAsync(id, first.RootElement, token);
        var secondTask = new CatalogDocumentRepository(secondContext).CreateAsync(id, second.RootElement, token);
        await transaction.CommitAsync(token);
        var results = await Task.WhenAll(firstTask, secondTask).WaitAsync(TimeSpan.FromSeconds(20), token);
        Assert.Equal(expectedSuccesses, results.Count(result => result.IsSuccess));
        if (expectedSuccesses == 1)
            Assert.Contains(Assert.Single(results, result => result.IsFailure).Errors,
                error => error.Code is "ExpectedInt" or "ExpectedString");
        await using var reader = database.CreateContext();
        var field = Assert.Single(await reader.FieldDefinitions.Where(field => field.CatalogId == id).ToListAsync(token));
        var documents = await reader.Documents.Where(record => record.CatalogId == id).ToListAsync(token);
        Assert.Equal(expectedSuccesses, documents.Count);
        foreach (var record in documents)
        {
            using var data = JsonDocument.Parse(record.Data);
            Assert.Equal(field.Type == CatalogFieldType.Int ? JsonValueKind.Number : JsonValueKind.String,
                data.RootElement.GetProperty("field").ValueKind);
        }
    }

    [Fact]
    public async Task DifferentCatalog_DoesNotWaitForUnrelatedCatalogLock()
    {
        var firstId = await CreateCatalogAsync();
        var secondId = await CreateCatalogAsync();
        var token = TestContext.Current.CancellationToken;
        await using var blocker = database.CreateContext();
        await using var transaction = await blocker.Database.BeginTransactionAsync(token);
        await blocker.Database.SqlQuery<Guid>($"SELECT id AS \"Value\" FROM catalog.catalogs WHERE id = {firstId} FOR UPDATE").ToListAsync(token);
        await using var writer = database.CreateContext();
        using var document = JsonDocument.Parse("{\"field\":true}");
        var result = await new CatalogDocumentRepository(writer).CreateAsync(secondId, document.RootElement, token)
            .WaitAsync(TimeSpan.FromSeconds(10), token);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task MissingCatalog_ReturnsNotFound()
    {
        await using var context = database.CreateContext();
        using var document = JsonDocument.Parse("{\"field\":true}");
        var result = await new CatalogDocumentRepository(context).CreateAsync(Guid.NewGuid(), document.RootElement, TestContext.Current.CancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal("Catalog.NotFound", Assert.Single(result.Errors).Code);
    }

    private async Task<Guid> CreateCatalogAsync()
    {
        await using var context = database.CreateContext();
        var result = await new CatalogRepository(context).CreateAsync("Documents-" + Guid.NewGuid(), TestContext.Current.CancellationToken);
        return result.Value;
    }

    private sealed class FailAfterSave : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated failure after SQL was executed.");
    }
}
