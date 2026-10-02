using System.Net.Http.Json;
using System.Text.Json;
using Findora.Catalog.Api;
using Findora.Catalog.Api.Contracts;
using Findora.Catalog.Infrastructure.Persistence;
using Findora.Catalog.Infrastructure.Persistence.Entities;
using Findora.Catalog.Core.Repository;
using Findora.Shated.Infrastructure.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Findora.Catalog.Api.Tests.Endpoints;

public sealed class CatalogListIntegrationTests
{
    [Fact]
    public async Task List_PaginatesDatabaseWithStableOrderingAndMetadataOnly()
    {
        var token = TestContext.Current.CancellationToken;
        await using var container = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await container.StartAsync(token);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Findora"] = container.GetConnectionString()
        });
        builder.Services.AddModule<CatalogModule>(builder.Configuration);
        await using var app = builder.Build();
        app.MapModules();
        await app.Services.InitializeModulesAsync(token);
        await app.StartAsync(token);
        using var client = app.GetTestClient();

        var empty = await client.GetFromJsonAsync<GetCatalogsResponse>("/api/catalog/catalogs", token);
        Assert.Empty(empty!.Items);
        Assert.Equal(0, empty.TotalCount);
        Assert.Equal(0, empty.TotalPages);

        var now = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        var records = Enumerable.Range(1, 23).Select(index => new CatalogRecord
        {
            Id = Guid.Parse(FormattableString.Invariant($"00000000-0000-0000-0000-{index:D12}")),
            Name = "Catalog-" + index,
            CreatedAt = index == 23 ? now.AddDays(-1) : now
        }).ToArray();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            context.Catalogs.AddRange(records);
            await context.SaveChangesAsync(token);
        }

        var expected = records.OrderByDescending(record => record.CreatedAt).ThenByDescending(record => record.Id).ToArray();
        var allIds = new List<Guid>();
        for (var page = 1; page <= 3; page++)
        {
            var body = await client.GetFromJsonAsync<GetCatalogsResponse>("/api/catalog/catalogs?page=" + page, token);
            Assert.Equal(page, body!.Page);
            Assert.Equal(10, body.PageSize);
            Assert.Equal(23, body.TotalCount);
            Assert.Equal(3, body.TotalPages);
            Assert.Equal(page == 3 ? 3 : 10, body.Items.Count);
            allIds.AddRange(body.Items.Select(item => item.Id));
        }
        Assert.Equal(expected.Select(record => record.Id), allIds);

        foreach (var page in new[] { 4, int.MaxValue })
        {
            var body = await client.GetFromJsonAsync<GetCatalogsResponse>("/api/catalog/catalogs?page=" + page, token);
            Assert.Empty(body!.Items);
            Assert.Equal(23, body.TotalCount);
            Assert.Equal(3, body.TotalPages);
        }

        var maximum = await client.GetFromJsonAsync<GetCatalogsResponse>("/api/catalog/catalogs?pageSize=100", token);
        Assert.Equal(23, maximum!.Items.Count);
        Assert.Equal(1, maximum.TotalPages);
        var json = await client.GetFromJsonAsync<JsonElement>("/api/catalog/catalogs?pageSize=1", token);
        var item = Assert.Single(json.GetProperty("items").EnumerateArray());
        Assert.Equal(expected[0].Id, item.GetProperty("id").GetGuid());
        Assert.Equal(expected[0].Name, item.GetProperty("name").GetString());
        Assert.Equal(now, item.GetProperty("createdAt").GetDateTimeOffset());
        Assert.Equal(3, item.EnumerateObject().Count());

        await using var readerScope = app.Services.CreateAsyncScope();
        var reader = readerScope.ServiceProvider.GetRequiredService<ICatalogRepository>();
        await reader.GetPageAsync(1, 10, token);
        Assert.Empty(readerScope.ServiceProvider.GetRequiredService<CatalogDbContext>().ChangeTracker.Entries());
    }
}
