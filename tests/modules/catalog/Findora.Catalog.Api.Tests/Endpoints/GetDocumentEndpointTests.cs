using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Findora.Catalog.Api.Contracts;
using Findora.Shated.Infrastructure.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Findora.Catalog.Api.Tests.Endpoints;

public sealed class GetDocumentEndpointTests
{
    private static readonly string[] Tags = ["new", "sale"];

    [Theory]
    [InlineData("")]
    [InlineData("/findora")]
    public async Task GetDocument_RoundTripsLocationAndEnforcesCatalogMembership(string pathBase)
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
        builder.Services.AddOpenApi();
        builder.Services.AddModule<CatalogModule>(builder.Configuration);
        await using var app = builder.Build();
        app.UsePathBase(pathBase);
        app.MapModules();
        app.MapOpenApi();
        await app.Services.InitializeModulesAsync(token);
        await app.StartAsync(token);
        using var client = app.GetTestClient();
        var catalogsRoute = pathBase + "/api/catalog/catalogs";
        using var catalogResponse = await client.PostAsJsonAsync(catalogsRoute, new CreateCatalogRequest("Books"), token);
        var catalog = await catalogResponse.Content.ReadFromJsonAsync<CreateCatalogResponse>(token);
        using var otherResponse = await client.PostAsJsonAsync(catalogsRoute, new CreateCatalogRequest("Articles"), token);
        var otherCatalog = await otherResponse.Content.ReadFromJsonAsync<CreateCatalogResponse>(token);
        var beforeCreation = DateTimeOffset.UtcNow.AddSeconds(-1);
        using var created = await client.PostAsJsonAsync(catalogsRoute + "/" + catalog!.Id + "/documents",
            new { title = "Book", count = 2, price = 12.5m, active = true, tags = Tags }, token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var creation = await created.Content.ReadFromJsonAsync<CreateDocumentResponse>(token);
        Assert.NotNull(created.Headers.Location);
        Assert.EndsWith(catalogsRoute + "/" + catalog.Id + "/documents/" + creation!.Id,
            created.Headers.Location.OriginalString, StringComparison.Ordinal);

        using var fetched = await client.GetAsync(created.Headers.Location, token);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        var body = await fetched.Content.ReadFromJsonAsync<JsonElement>(token);
        Assert.Equal(["catalogId", "createdAt", "data", "id"], body.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(creation.Id, body.GetProperty("id").GetGuid());
        Assert.Equal(catalog.Id, body.GetProperty("catalogId").GetGuid());
        var createdAt = body.GetProperty("createdAt").GetDateTimeOffset();
        Assert.Equal(TimeSpan.Zero, createdAt.Offset);
        Assert.InRange(createdAt, beforeCreation, DateTimeOffset.UtcNow);
        var data = body.GetProperty("data");
        Assert.Equal(JsonValueKind.Object, data.ValueKind);
        Assert.Equal("Book", data.GetProperty("title").GetString());
        Assert.Equal(2, data.GetProperty("count").GetInt32());
        Assert.Equal(12.5m, data.GetProperty("price").GetDecimal());
        Assert.True(data.GetProperty("active").GetBoolean());
        Assert.Equal(Tags, data.GetProperty("tags").EnumerateArray().Select(value => value.GetString()));

        foreach (var (catalogId, documentId) in new[]
        {
            (otherCatalog!.Id, creation.Id), (Guid.NewGuid(), creation.Id), (catalog.Id, Guid.NewGuid())
        })
        {
            using var missing = await client.GetAsync(new Uri(catalogsRoute + "/" + catalogId + "/documents/" + documentId, UriKind.Relative), token);
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            Assert.Equal("application/problem+json", missing.Content.Headers.ContentType!.MediaType);
            var problem = await missing.Content.ReadFromJsonAsync<JsonElement>(token);
            var error = Assert.Single(problem.GetProperty("errors").EnumerateArray());
            Assert.Equal("Document.NotFound", error.GetProperty("code").GetString());
            Assert.Equal("documentId", error.GetProperty("path").GetString());
        }

        foreach (var invalid in new[] { "bad-id", Guid.Empty.ToString() })
        {
            foreach (var (catalogId, documentId, expectedPaths) in new[]
            {
                (invalid, creation.Id.ToString(), new[] { "catalogId" }),
                (catalog.Id.ToString(), invalid, new[] { "documentId" }),
                (invalid, invalid, new[] { "catalogId", "documentId" })
            })
            {
                using var response = await client.GetAsync(new Uri(catalogsRoute + "/" + catalogId + "/documents/" + documentId, UriKind.Relative), token);
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
                var problem = await response.Content.ReadFromJsonAsync<JsonElement>(token);
                var errors = problem.GetProperty("errors").EnumerateArray().ToArray();
                Assert.Equal(expectedPaths, errors.Select(error => error.GetProperty("path").GetString()));
                Assert.Equal(expectedPaths.Select(path => path == "catalogId" ? "Catalog.InvalidId" : "Document.InvalidId"),
                    errors.Select(error => error.GetProperty("code").GetString()));
            }
        }

        var openApi = await client.GetFromJsonAsync<JsonElement>(pathBase + "/openapi/v1.json", token);
        var operation = openApi.GetProperty("paths")
            .GetProperty("/api/catalog/catalogs/{catalogId}/documents/{documentId}").GetProperty("get");
        Assert.Equal("catalog.GetDocument", operation.GetProperty("operationId").GetString());
        foreach (var status in new[] { "200", "400", "404" })
            Assert.True(operation.GetProperty("responses").TryGetProperty(status, out _));
    }
}
