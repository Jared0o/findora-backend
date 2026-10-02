using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Findora.Catalog.Infrastructure.Persistence;

public sealed class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Findora")
            ?? "Host=localhost;Port=54320;Database=findora;Username=findora;Password=findora_dev";
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(connectionString, postgres => postgres.MigrationsHistoryTable(
                CatalogDbContext.MigrationsHistoryTable, CatalogDbContext.SchemaName));
        return new CatalogDbContext(options.Options);
    }
}
