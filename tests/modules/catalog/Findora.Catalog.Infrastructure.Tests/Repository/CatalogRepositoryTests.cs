using Findora.Catalog.Core;
using Findora.Catalog.Core.Commands.CreateCatalog;
using Findora.Catalog.Infrastructure.Persistence;
using Findora.Catalog.Infrastructure.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Findora.Catalog.Infrastructure.Tests.Repository;

[Collection("PostgreSQL")]
public sealed class CatalogRepositoryTests(PostgresFixture database)
{
    [Fact]
    public async Task CreateAsync_PersistsCatalogWithIdAndUtcTimestamp()
    {
        var name = "Catalog-" + Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;
        Guid createdId;
        await using (var context = database.CreateContext())
        {
            var result = await new CatalogRepository(context).CreateAsync(name, TestContext.Current.CancellationToken);
            Assert.True(result.IsSuccess);
            createdId = result.Value;
        }

        await using var reader = database.CreateContext();
        var catalog = await reader.Catalogs.SingleAsync(record => record.Name == name,
            TestContext.Current.CancellationToken);
        Assert.Equal(createdId, catalog.Id);
        Assert.Equal(7, catalog.Id.Version);
        Assert.Equal(TimeSpan.Zero, catalog.CreatedAt.Offset);
        Assert.InRange(catalog.CreatedAt, startedAt.AddSeconds(-1), DateTimeOffset.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task ExecuteAsync_UsesRegisteredRepositoryAndPersistsTrimmedName()
    {
        var name = "Command-" + Guid.NewGuid();
        var services = new ServiceCollection();
        services.AddCatalogCore();
        services.AddCatalogInfrastructure(database.ConnectionString);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        await using (var scope = provider.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<CreateCatalogCommandHandler>();
            var result = await handler.ExecuteAsync(new CreateCatalogCommand(" " + name + " "),
                TestContext.Current.CancellationToken);
            Assert.True(result.IsSuccess);
        }

        await using var reader = database.CreateContext();
        Assert.True(await reader.Catalogs.AnyAsync(record => record.Name == name,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateAsync_DoesNotTrackOrPersistCatalogWhenCancelled()
    {
        await using var context = database.CreateContext();
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        var repository = new CatalogRepository(context);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.CreateAsync("Cancelled", source.Token));

        Assert.Empty(context.ChangeTracker.Entries());
    }
}
