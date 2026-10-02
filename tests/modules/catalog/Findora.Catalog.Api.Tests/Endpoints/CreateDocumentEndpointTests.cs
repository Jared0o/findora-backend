using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Findora.Catalog.Api;
using Findora.Catalog.Api.Contracts;
using Findora.Catalog.Infrastructure.Persistence;
using Findora.Shated.Infrastructure.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Findora.Catalog.Api.Tests.Endpoints;

public sealed class CreateDocumentEndpointTests
{
    [Fact]
    public async Task CreateDocument_DiscoversSchemaAndRejectsInvalidInputAtomically()
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
        app.MapModules();
        app.MapOpenApi();
        await app.Services.InitializeModulesAsync(token);
        await app.StartAsync(token);
        using var client = app.GetTestClient();
        using var catalogResponse = await client.PostAsJsonAsync("/api/catalog/catalogs", new CreateCatalogRequest("Articles"), token);
        var catalog = await catalogResponse.Content.ReadFromJsonAsync<CreateCatalogResponse>(token);
        var route = "/api/catalog/catalogs/" + catalog!.Id + "/documents";
        using var payload = new StringContent("{\"title\":\"News\",\"score\":10.0,\"tags\":[\"world\"]}", Encoding.UTF8, "application/json");
        using var created = await client.PostAsync(route, payload, token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Null(created.Headers.Location);
        var document = await created.Content.ReadFromJsonAsync<CreateDocumentResponse>(token);
        Assert.Equal(7, document!.Id.Version);
        var details = await client.GetFromJsonAsync<GetCatalogResponse>("/api/catalog/catalogs/" + catalog.Id, token);
        Assert.Equal(["score", "tags", "title"], details!.Fields.Select(field => field.Name));
        Assert.Equal("decimal", details.Fields[0].Type);
        Assert.True(details.Fields[1].IsArray);
        Assert.All(details.Fields, field => Assert.False(field.IsRequired));

        foreach (var json in new[]
        {
            "{}", "[]", "null", "{\"new\":[]}", "{\"new\":null}", "{\"new\":{}}",
            "{\"new\":[[1]]}", "{\"new\":1,\"new\":2}", "{\"score\":\"wrong\",\"new\":true}"
        })
        {
            using var invalid = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await client.PostAsync(route, invalid, token);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(token);
            Assert.NotEmpty(problem.GetProperty("errors").EnumerateArray());
        }
        using var invalidId = await client.PostAsJsonAsync("/api/catalog/catalogs/invalid/documents", new { title = "test" }, token);
        Assert.Equal(HttpStatusCode.BadRequest, invalidId.StatusCode);
        using var missing = await client.PostAsJsonAsync("/api/catalog/catalogs/" + Guid.NewGuid() + "/documents", new { title = "test" }, token);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var missingProblem = await missing.Content.ReadFromJsonAsync<JsonElement>(token);
        Assert.Equal("Catalog.NotFound", Assert.Single(missingProblem.GetProperty("errors").EnumerateArray()).GetProperty("code").GetString());

        await using var scope = app.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var saved = Assert.Single(await context.Documents.ToListAsync(token));
        Assert.Equal(document.Id, saved.Id);
        Assert.Equal(3, await context.FieldDefinitions.CountAsync(token));

        var openApi = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json", token);
        var operation = openApi.GetProperty("paths").GetProperty("/api/catalog/catalogs/{catalogId}/documents").GetProperty("post");
        Assert.Equal("catalog.CreateDocument", operation.GetProperty("operationId").GetString());
        foreach (var status in new[] { "201", "400", "404" })
            Assert.True(operation.GetProperty("responses").TryGetProperty(status, out _));
    }
}
