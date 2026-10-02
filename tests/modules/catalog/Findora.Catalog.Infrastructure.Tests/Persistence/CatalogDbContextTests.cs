using Findora.Catalog.Core.Models;
using Findora.Catalog.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Findora.Catalog.Infrastructure.Tests.Persistence;

[Collection("PostgreSQL")]
public sealed class CatalogDbContextTests(PostgresFixture database)
{
    [Fact]
    public async Task Migrations_CreateModuleTablesAndHistoryInCatalogSchema()
    {
        await using var context = database.CreateContext();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);

        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Empty(await context.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
        await context.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT COUNT(*) FROM information_schema.tables
            WHERE table_schema = 'catalog'
              AND table_name IN ('catalogs', 'field_definitions', '__EFMigrationsHistory')
            """;
        Assert.Equal(3L, await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
        command.CommandText = """
            SELECT COUNT(*) FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_name IN ('catalogs', 'field_definitions', '__EFMigrationsHistory')
            """;
        Assert.Equal(0L, await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FieldDefinitions_AllowDifferentTypesForSameNameInDifferentCatalogs()
    {
        var first = CreateCatalog();
        var second = CreateCatalog();
        first.Fields.Add(CreateField(first.Id, CatalogFieldType.Int));
        second.Fields.Add(CreateField(second.Id, CatalogFieldType.String));
        await using (var writer = database.CreateContext())
        {
            writer.Catalogs.AddRange(first, second);
            await writer.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var reader = database.CreateContext();
        var fields = await reader.FieldDefinitions.Where(field => field.CatalogId == first.Id || field.CatalogId == second.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, fields.Count);
        Assert.Contains(fields, field => field.CatalogId == first.Id && field.Type == CatalogFieldType.Int);
        Assert.Contains(fields, field => field.CatalogId == second.Id && field.Type == CatalogFieldType.String);
    }

    [Fact]
    public async Task FieldDefinitions_RejectUnknownCatalog()
    {
        await using var context = database.CreateContext();
        context.FieldDefinitions.Add(CreateField(Guid.NewGuid(), CatalogFieldType.String));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            context.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
    }

    [Fact]
    public async Task FieldDefinitions_RejectUnsupportedTypeAndRollbackWholeSave()
    {
        var catalog = CreateCatalog();
        catalog.Fields.Add(CreateField(catalog.Id, (CatalogFieldType)999));
        await using (var writer = database.CreateContext())
        {
            writer.Catalogs.Add(catalog);
            var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
                writer.SaveChangesAsync(TestContext.Current.CancellationToken));
            Assert.Equal(PostgresErrorCodes.CheckViolation, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
        }

        await using var reader = database.CreateContext();
        Assert.False(await reader.Catalogs.AnyAsync(record => record.Id == catalog.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FieldDefinitions_EnforceOneTypeDuringConcurrentDiscovery()
    {
        var catalog = CreateCatalog();
        await using (var setup = database.CreateContext())
        {
            setup.Catalogs.Add(catalog);
            await setup.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var outcomes = await Task.WhenAll(
            TryInsertFieldAsync(catalog.Id, CatalogFieldType.Int),
            TryInsertFieldAsync(catalog.Id, CatalogFieldType.String));

        Assert.Single(outcomes, outcome => outcome is null);
        Assert.Single(outcomes, outcome => outcome == PostgresErrorCodes.UniqueViolation);
        await using var reader = database.CreateContext();
        Assert.Single(await reader.FieldDefinitions.Where(field => field.CatalogId == catalog.Id)
            .ToListAsync(TestContext.Current.CancellationToken));
    }

    private async Task<string?> TryInsertFieldAsync(Guid catalogId, CatalogFieldType type)
    {
        await using var context = database.CreateContext();
        context.FieldDefinitions.Add(CreateField(catalogId, type));
        try
        {
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            return null;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres)
        {
            return postgres.SqlState;
        }
    }

    private static CatalogRecord CreateCatalog() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Fields-" + Guid.NewGuid(),
        CreatedAt = DateTimeOffset.UtcNow
    };

    private static CatalogFieldRecord CreateField(Guid catalogId, CatalogFieldType type) => new()
    {
        CatalogId = catalogId,
        Name = "size",
        Type = type,
        IsArray = false,
        IsRequired = true
    };
}
