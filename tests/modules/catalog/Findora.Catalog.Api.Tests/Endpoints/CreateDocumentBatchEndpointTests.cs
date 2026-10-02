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

public sealed class CreateDocumentBatchEndpointTests
{
    [Fact]
    public async Task Batch_CreatesDocumentsAndReturnsIndexedErrorsWithoutPartialWrites()
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
        using var createdCatalog = await client.PostAsJsonAsync("/api/catalog/catalogs", new CreateCatalogRequest("Batch"), token);
        var catalog = await createdCatalog.Content.ReadFromJsonAsync<CreateCatalogResponse>(token);
        var route = new Uri("/api/catalog/catalogs/" + catalog!.Id + "/documents/batch", UriKind.Relative);
        using var payload = new StringContent("[{\"title\":\"first\",\"score\":10.0},{\"title\":\"second\",\"score\":20,\"published\":true}]", Encoding.UTF8, "application/json");
        using var created = await client.PostAsync(route, payload, token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Null(created.Headers.Location);
        var result = await created.Content.ReadFromJsonAsync<CreateDocumentBatchResponse>(token);
        Assert.Equal(2, result!.CreatedCount);
        Assert.Equal(2, result.Ids.Count);
        var details = await client.GetFromJsonAsync<GetCatalogResponse>("/api/catalog/catalogs/" + catalog.Id, token);
        Assert.Equal(["published", "score", "title"], details!.Fields.Select(field => field.Name));

        using var invalid = new StringContent("[{\"fresh\":true},{\"score\":\"wrong\"},{\"bad\":null}]", Encoding.UTF8, "application/json");
        using var rejected = await client.PostAsync(route, invalid, token);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType!.MediaType);
        var problem = await rejected.Content.ReadFromJsonAsync<JsonElement>(token);
        Assert.Equal(["documents[1].score", "documents[2].bad"], problem.GetProperty("errors").EnumerateArray().Select(error => error.GetProperty("path").GetString()));
        foreach (var body in new[] { "{}", "null", "[]", "[null]", "[{}]", "[" + string.Join(",", Enumerable.Repeat("{}", 101)) + "]" })
        {
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await client.PostAsync(route, content, token);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        }
        using var badId = await client.PostAsJsonAsync("/api/catalog/catalogs/invalid/documents/batch", new[] { new { x = 1 } }, token);
        Assert.Equal(HttpStatusCode.BadRequest, badId.StatusCode);
        using var missing = await client.PostAsJsonAsync("/api/catalog/catalogs/" + Guid.NewGuid() + "/documents/batch", new[] { new { x = 1 } }, token);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        await using var scope = app.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var saved = await context.Documents.ToDictionaryAsync(document => document.Id, token);
        Assert.Equal(2, saved.Count);
        for (var index = 0; index < result.Ids.Count; index++)
        {
            using var data = JsonDocument.Parse(saved[result.Ids[index]].Data);
            Assert.Equal(index == 0 ? "first" : "second", data.RootElement.GetProperty("title").GetString());
        }
        Assert.Equal(3, await context.FieldDefinitions.CountAsync(token));
        using var maximum = await client.PostAsJsonAsync(route, Enumerable.Range(1, 100).Select(index => new { index }).ToArray(), token);
        Assert.Equal(HttpStatusCode.Created, maximum.StatusCode);
        Assert.Equal(100, (await maximum.Content.ReadFromJsonAsync<CreateDocumentBatchResponse>(token))!.CreatedCount);

        var openApi = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json", token);
        var operation = openApi.GetProperty("paths").GetProperty("/api/catalog/catalogs/{catalogId}/documents/batch").GetProperty("post");
        Assert.Equal("catalog.CreateDocumentBatch", operation.GetProperty("operationId").GetString());
        foreach (var status in new[] { "201", "400", "404" })
            Assert.True(operation.GetProperty("responses").TryGetProperty(status, out _));
    }
}
