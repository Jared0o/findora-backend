using Findora.Catalog.Core;
using Findora.Catalog.Api.Endpoints.Documents;
using Findora.Catalog.Api.Endpoints.Catalogs;
using Findora.Catalog.Infrastructure;
using Findora.Shared.Abstraction.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Findora.Catalog.Api;

public sealed class CatalogModule : IModule
{
    public string Name => "catalog";
    public string RoutePrefix => "/api/catalog";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        var connectionString = configuration.GetConnectionString("Findora");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Configure ConnectionStrings:Findora for PostgreSQL.");
        }

        services.AddCatalogCore();
        services.AddCatalogInfrastructure(connectionString);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        CreateCatalogEndpoint.Map(endpoints);
        GetCatalogEndpoint.Map(endpoints);
        GetCatalogsEndpoint.Map(endpoints);
        CreateDocumentEndpoint.Map(endpoints);
        CreateDocumentBatchEndpoint.Map(endpoints);
    }

    public Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
        => services.MigrateCatalogDatabaseAsync(cancellationToken);
}
