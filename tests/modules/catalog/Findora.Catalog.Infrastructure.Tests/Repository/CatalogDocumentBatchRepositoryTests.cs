using System.Text.Json;
using Findora.Catalog.Infrastructure.Persistence;
using Findora.Catalog.Infrastructure.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Findora.Catalog.Infrastructure.Tests.Repository;

[Collection("PostgreSQL")]
public sealed class CatalogDocumentBatchRepositoryTests(PostgresFixture database)
{
    [Fact]
    public async Task Batch_PersistsOneHundredDocumentsAndReturnsIdsInInputOrder()
    {
        var token = TestContext.Current.CancellationToken;
        var id = await CreateCatalogAsync();
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(Enumerable.Range(0, 100).Select(index => new { index })));
        await using var writer = database.CreateContext();
        var result = await new CatalogDocumentRepository(writer).CreateBatchAsync(id, json.RootElement.EnumerateArray().ToArray(), token);
        Assert.True(result.IsSuccess);
        Assert.Equal(100, result.Value.Count);
        Assert.Equal(100, result.Value.Distinct().Count());
        Assert.Empty(writer.ChangeTracker.Entries());
        await using var reader = database.CreateContext();
        var records = await reader.Documents.Where(record => record.CatalogId == id).ToDictionaryAsync(record => record.Id, token);
        Assert.Equal(100, records.Count);
        for (var index = 0; index < 100; index++)
        {
            Assert.Equal(7, result.Value[index].Version);
            using var data = JsonDocument.Parse(records[result.Value[index]].Data);
            Assert.Equal(index, data.RootElement.GetProperty("index").GetInt32());
        }
        Assert.Single(await reader.FieldDefinitions.Where(field => field.CatalogId == id).ToListAsync(token));
    }

    [Fact]
    public async Task Batch_ValidationFailureLeavesNoDocumentsOrFields()
    {
        var token = TestContext.Current.CancellationToken;
        var id = await CreateCatalogAsync();
        using var json = JsonDocument.Parse("[{\"count\":1},{\"count\":\"wrong\",\"fresh\":true},{\"other\":null}]");
        await using var writer = database.CreateContext();
        var result = await new CatalogDocumentRepository(writer).CreateBatchAsync(id, json.RootElement.EnumerateArray().ToArray(), token);
        Assert.True(result.IsFailure);
        Assert.Equal(["documents[1].count", "documents[2].other"], result.Errors.Select(error => error.Path));
        Assert.Empty(writer.ChangeTracker.Entries());
        await using var reader = database.CreateContext();
        Assert.False(await reader.Documents.AnyAsync(record => record.CatalogId == id, token));
        Assert.False(await reader.FieldDefinitions.AnyAsync(field => field.CatalogId == id, token));
    }

    [Fact]
    public async Task Batch_SaveFailureRollsBackWholeBatchAndCanBeRetried()
    {
        var token = TestContext.Current.CancellationToken;
        var id = await CreateCatalogAsync();
        using var json = JsonDocument.Parse("[{\"first\":1},{\"second\":true}]");
        var documents = json.RootElement.EnumerateArray().ToArray();
        var options = new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(database.ConnectionString)
            .AddInterceptors(new FailAfterSave()).Options;
        await using (var writer = new CatalogDbContext(options))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => new CatalogDocumentRepository(writer).CreateBatchAsync(id, documents, token));
            Assert.Empty(writer.ChangeTracker.Entries());
        }
        await using var reader = database.CreateContext();
        Assert.False(await reader.Documents.AnyAsync(record => record.CatalogId == id, token));
        Assert.False(await reader.FieldDefinitions.AnyAsync(field => field.CatalogId == id, token));
        Assert.True((await new CatalogDocumentRepository(reader).CreateBatchAsync(id, documents, token)).IsSuccess);
        Assert.Equal(2, await reader.Documents.CountAsync(record => record.CatalogId == id, token));
    }

    [Theory]
    [InlineData("3", true)]
    [InlineData("\"text\"", false)]
    public async Task ConcurrentBatchAndSingleWrite_ShareTheCatalogLock(string singleValue, bool compatible)
    {
        var id = await CreateCatalogAsync();
        var token = TestContext.Current.CancellationToken;
        using var batch = JsonDocument.Parse("[{\"field\":1},{\"field\":2}]");
        using var single = JsonDocument.Parse("{\"field\":" + singleValue + "}");
        await using var batchContext = database.CreateContext();
        await using var singleContext = database.CreateContext();
        await using var blocker = database.CreateContext();
        await using var transaction = await blocker.Database.BeginTransactionAsync(token);
        await blocker.Database.SqlQuery<Guid>($"SELECT id AS \"Value\" FROM catalog.catalogs WHERE id = {id} FOR UPDATE").ToListAsync(token);
        var batchTask = new CatalogDocumentRepository(batchContext).CreateBatchAsync(id, batch.RootElement.EnumerateArray().ToArray(), token);
        var singleTask = new CatalogDocumentRepository(singleContext).CreateAsync(id, single.RootElement, token);
        await transaction.CommitAsync(token);
        await Task.WhenAll(batchTask, singleTask).WaitAsync(TimeSpan.FromSeconds(20), token);
        var batchResult = await batchTask;
        var singleResult = await singleTask;
        if (compatible)
        {
            Assert.True(batchResult.IsSuccess);
            Assert.True(singleResult.IsSuccess);
        }
        else Assert.NotEqual(batchResult.IsSuccess, singleResult.IsSuccess);
        await using var reader = database.CreateContext();
        Assert.Equal((batchResult.IsSuccess ? 2 : 0) + (singleResult.IsSuccess ? 1 : 0),
            await reader.Documents.CountAsync(record => record.CatalogId == id, token));
        Assert.Single(await reader.FieldDefinitions.Where(field => field.CatalogId == id).ToListAsync(token));
    }

    private async Task<Guid> CreateCatalogAsync()
    {
        await using var context = database.CreateContext();
        return (await new CatalogRepository(context).CreateAsync("Batch-" + Guid.NewGuid(), TestContext.Current.CancellationToken)).Value;
    }

    private sealed class FailAfterSave : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated failure after SQL execution.");
    }
}
