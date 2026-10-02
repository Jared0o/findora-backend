using Findora.Catalog.Infrastructure.Persistence.Entities;
using Findora.Catalog.Infrastructure.Repository;

namespace Findora.Catalog.Infrastructure.Tests.Repository;

[Collection("PostgreSQL")]
public sealed class CatalogDocumentListTests(PostgresFixture database)
{
    [Fact]
    public async Task GetPage_FiltersCountsAndOrdersDocumentsWithoutTracking()
    {
        var token = TestContext.Current.CancellationToken;
        var catalogId = Guid.CreateVersion7();
        var otherId = Guid.CreateVersion7();
        var now = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        var records = Enumerable.Range(1, 23).Select(index => new DocumentRecord
        {
            Id = Guid.CreateVersion7(), CatalogId = catalogId,
            CreatedAt = index == 23 ? now.AddDays(-1) : now,
            Data = "{\"title\":\"Book\",\"price\":12.5,\"active\":true,\"tags\":[\"new\"],\"empty\":[]}"
        }).ToArray();
        await using (var writer = database.CreateContext())
        {
            writer.Catalogs.AddRange(
                new CatalogRecord { Id = catalogId, Name = "List-" + catalogId, CreatedAt = now },
                new CatalogRecord { Id = otherId, Name = "List-" + otherId, CreatedAt = now });
            writer.Documents.AddRange(records);
            writer.Documents.Add(new DocumentRecord
            {
                Id = Guid.CreateVersion7(), CatalogId = otherId, CreatedAt = now.AddDays(1), Data = "{\"other\":true}"
            });
            await writer.SaveChangesAsync(token);
        }

        var expected = records.OrderByDescending(record => record.CreatedAt).ThenByDescending(record => record.Id).ToArray();
        await using var reader = database.CreateContext();
        var repository = new CatalogDocumentRepository(reader);
        var allIds = new List<Guid>();
        for (var page = 1; page <= 3; page++)
        {
            var result = await repository.GetPageAsync(catalogId, page, 10, token);
            Assert.NotNull(result);
            Assert.Equal(page, result.Page);
            Assert.Equal(10, result.PageSize);
            Assert.Equal(23, result.TotalCount);
            Assert.Equal(3, result.TotalPages);
            Assert.Equal(page == 3 ? 3 : 10, result.Items.Count);
            Assert.All(result.Items, item =>
            {
                Assert.Equal(catalogId, item.CatalogId);
                Assert.Equal(TimeSpan.Zero, item.CreatedAt.Offset);
                Assert.Equal("Book", item.Data.GetProperty("title").GetString());
                Assert.Equal(12.5m, item.Data.GetProperty("price").GetDecimal());
                Assert.True(item.Data.GetProperty("active").GetBoolean());
                Assert.Equal("new", item.Data.GetProperty("tags")[0].GetString());
                Assert.Empty(item.Data.GetProperty("empty").EnumerateArray());
            });
            allIds.AddRange(result.Items.Select(item => item.Id));
        }
        Assert.Equal(expected.Select(record => record.Id), allIds);
        Assert.Empty(reader.ChangeTracker.Entries());

        foreach (var page in new[] { 4, int.MaxValue })
        {
            var result = await repository.GetPageAsync(catalogId, page, 10, token);
            Assert.NotNull(result);
            Assert.Empty(result.Items);
            Assert.Equal(page, result.Page);
            Assert.Equal(23, result.TotalCount);
            Assert.Equal(3, result.TotalPages);
        }
    }

    [Fact]
    public async Task GetPage_DistinguishesEmptyAndMissingCatalog()
    {
        var token = TestContext.Current.CancellationToken;
        await using var writer = database.CreateContext();
        var created = await new CatalogRepository(writer).CreateAsync("Empty-" + Guid.NewGuid(), token);
        await using var reader = database.CreateContext();
        var repository = new CatalogDocumentRepository(reader);
        var empty = await repository.GetPageAsync(created.Value, 1, 10, token);
        Assert.NotNull(empty);
        Assert.Empty(empty.Items);
        Assert.Equal(0, empty.TotalCount);
        Assert.Equal(0, empty.TotalPages);
        Assert.Null(await repository.GetPageAsync(Guid.NewGuid(), 1, 10, token));
        Assert.Empty(reader.ChangeTracker.Entries());
    }
}
