using Findora.Catalog.Core.Models;
using Findora.Catalog.Infrastructure.Persistence.Entities;
using Findora.Catalog.Infrastructure.Repository;

namespace Findora.Catalog.Infrastructure.Tests.Repository;

[Collection("PostgreSQL")]
public sealed class CatalogReadTests(PostgresFixture database)
{
    [Fact]
    public async Task GetById_ReturnsOnlyRequestedCatalogWithOrderedFieldsAndDoesNotTrackEntities()
    {
        var token = TestContext.Current.CancellationToken;
        var id = Guid.CreateVersion7();
        var otherId = Guid.CreateVersion7();
        var createdAt = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        await using (var writer = database.CreateContext())
        {
            var record = new CatalogRecord { Id = id, Name = "Books", CreatedAt = createdAt };
            record.Fields.Add(new CatalogFieldRecord { CatalogId = id, Name = "tags", Type = CatalogFieldType.String, IsArray = true });
            record.Fields.Add(new CatalogFieldRecord { CatalogId = id, Name = "active", Type = CatalogFieldType.Bool, IsRequired = true });
            var other = new CatalogRecord { Id = otherId, Name = "Other", CreatedAt = createdAt };
            other.Fields.Add(new CatalogFieldRecord { CatalogId = otherId, Name = "tags", Type = CatalogFieldType.Int });
            writer.Catalogs.AddRange(record, other);
            await writer.SaveChangesAsync(token);
        }

        await using var reader = database.CreateContext();
        var repository = new CatalogRepository(reader);
        var catalog = await repository.GetByIdAsync(id, token);
        Assert.NotNull(catalog);
        Assert.Equal(id, catalog.Id);
        Assert.Equal("Books", catalog.Name);
        Assert.Equal(createdAt, catalog.CreatedAt);
        Assert.Equal(["active", "tags"], catalog.Fields.Select(field => field.Name));
        Assert.Equal(CatalogFieldType.Bool, catalog.Fields[0].Type);
        Assert.True(catalog.Fields[0].IsRequired);
        Assert.False(catalog.Fields[0].IsArray);
        Assert.Equal(CatalogFieldType.String, catalog.Fields[1].Type);
        Assert.True(catalog.Fields[1].IsArray);
        Assert.False(catalog.Fields[1].IsRequired);
        Assert.Empty(reader.ChangeTracker.Entries());
        Assert.Null(await repository.GetByIdAsync(Guid.NewGuid(), token));
    }
}
