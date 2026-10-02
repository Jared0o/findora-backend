using Findora.Catalog.Core.Repository;
using Findora.Catalog.Infrastructure.Persistence;
using Findora.Catalog.Infrastructure.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Findora.Catalog.Infrastructure;

public static class Extensions
{
    public static Task MigrateCatalogDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        cancellationToken.ThrowIfCancellationRequested();
        var context = services.GetRequiredService<CatalogDbContext>();
        return context.Database.MigrateAsync(cancellationToken);
    }

    public static IServiceCollection AddCatalogInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.AddDbContext<CatalogDbContext>(options => options.UseNpgsql(connectionString,
            postgres => postgres.MigrationsHistoryTable(
                CatalogDbContext.MigrationsHistoryTable, CatalogDbContext.SchemaName)));
        services.TryAddScoped<ICatalogRepository, CatalogRepository>();
        services.TryAddScoped<ICatalogDocumentRepository, CatalogDocumentRepository>();
        return services;
    }
}
