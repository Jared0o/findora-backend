using Findora.Catalog.Infrastructure.Persistence.Entities;
using Findora.Catalog.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Findora.Catalog.Infrastructure.Persistence;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public const string SchemaName = "catalog";
    public const string MigrationsHistoryTable = "__EFMigrationsHistory";

    public DbSet<CatalogRecord> Catalogs => Set<CatalogRecord>();
    public DbSet<CatalogFieldRecord> FieldDefinitions => Set<CatalogFieldRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfiguration(new CatalogRecordConfiguration());
        modelBuilder.ApplyConfiguration(new CatalogFieldRecordConfiguration());
    }
}
