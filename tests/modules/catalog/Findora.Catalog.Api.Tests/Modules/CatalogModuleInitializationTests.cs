using Findora.Catalog.Api;
using Findora.Catalog.Core.Commands.CreateCatalog;
using Findora.Catalog.Infrastructure.Persistence;
using Findora.Shated.Infrastructure.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Findora.Catalog.Api.Tests.Modules;

public sealed class CatalogModuleInitializationTests
{
    [Fact]
    public async Task InitializeModules_MigratesEmptyDatabaseAndPreservesDataOnRestart()
    {
        var token = TestContext.Current.CancellationToken;
        await using var container = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await container.StartAsync(token);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Findora"] = container.GetConnectionString()
        }).Build();
        var services = new ServiceCollection();
        services.AddModule<CatalogModule>(configuration);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            Assert.Empty(await context.Database.GetAppliedMigrationsAsync(token));
        }

        await provider.InitializeModulesAsync(token);
        Guid catalogId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            Assert.NotEmpty(await context.Database.GetAppliedMigrationsAsync(token));
            Assert.Empty(await context.Database.GetPendingMigrationsAsync(token));
            var handler = scope.ServiceProvider.GetRequiredService<CreateCatalogCommandHandler>();
            var result = await handler.ExecuteAsync(new CreateCatalogCommand("Books"), token);
            Assert.True(result.IsSuccess);
            catalogId = result.Value;
        }

        await provider.InitializeModulesAsync(token);

        await using var readerScope = provider.CreateAsyncScope();
        var reader = readerScope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var catalog = Assert.Single(await reader.Catalogs.ToListAsync(token));
        Assert.Equal(catalogId, catalog.Id);
        Assert.Equal("Books", catalog.Name);
        Assert.Empty(await reader.Database.GetPendingMigrationsAsync(token));
    }
}
