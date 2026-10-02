using System.Net;
using System.Text.Json;
using System.Net.Http.Json;
using Findora.Catalog.Api;
using Findora.Catalog.Api.Contracts;
using Findora.Shated.Infrastructure.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace Findora.Catalog.Api.Tests.Endpoints;

public sealed class CatalogRoundTripTests
{
    [Fact]
    public async Task Create_LocationReadsPersistedCatalogIncludingPathBase()
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
        app.UsePathBase("/findora");
        app.MapModules();
        await app.Services.InitializeModulesAsync(token);
        await app.StartAsync(token);
        using var client = app.GetTestClient();

        using var created = await client.PostAsJsonAsync("/findora/api/catalog/catalogs", new CreateCatalogRequest(" Books "), token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var creation = await created.Content.ReadFromJsonAsync<CreateCatalogResponse>(token);
        Assert.NotNull(created.Headers.Location);
        Assert.Contains("/findora/api/catalog/catalogs/", created.Headers.Location.OriginalString, StringComparison.Ordinal);

        using var fetched = await client.GetAsync(created.Headers.Location, token);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        var catalog = await fetched.Content.ReadFromJsonAsync<GetCatalogResponse>(token);
        Assert.Equal(creation!.Id, catalog!.Id);
        Assert.Equal("Books", catalog.Name);
        Assert.Equal(TimeSpan.Zero, catalog.CreatedAt.Offset);
        Assert.NotEqual(default, catalog.CreatedAt);
        Assert.Empty(catalog.Fields);

        using var duplicate = await client.PostAsJsonAsync("/findora/api/catalog/catalogs", new CreateCatalogRequest(" books "), token);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Null(duplicate.Headers.Location);
        Assert.Equal("application/problem+json", duplicate.Content.Headers.ContentType!.MediaType);
        var problem = await duplicate.Content.ReadFromJsonAsync<JsonElement>(token);
        Assert.Equal(409, problem.GetProperty("status").GetInt32());
        var error = Assert.Single(problem.GetProperty("errors").EnumerateArray());
        Assert.Equal("Catalog.NameAlreadyExists", error.GetProperty("code").GetString());
        Assert.Equal("name", error.GetProperty("path").GetString());
    }
}
