using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Findora.Catalog.Api.Contracts;
using Findora.Catalog.Infrastructure.Persistence;
using Findora.Catalog.Infrastructure.Persistence.Entities;
using Findora.Shated.Infrastructure.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Findora.Catalog.Api.Tests.Endpoints;

public sealed class GetDocumentsEndpointTests
{
    [Fact]
    public async Task List_ReturnsFullDocumentsWithPaginationValidationAndOpenApi()
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
        using var created = await client.PostAsJsonAsync("/api/catalog/catalogs", new CreateCatalogRequest("Documents"), token);
        var catalog = await created.Content.ReadFromJsonAsync<CreateCatalogResponse>(token);
        var route = "/api/catalog/catalogs/" + catalog!.Id + "/documents";

        var empty = await client.GetFromJsonAsync<GetDocumentsResponse>(route, token);
        Assert.NotNull(empty);
        Assert.Empty(empty.Items);
        Assert.Equal(1, empty.Page);
        Assert.Equal(10, empty.PageSize);
        Assert.Equal(0, empty.TotalCount);
        Assert.Equal(0, empty.TotalPages);

        var now = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        var records = Enumerable.Range(1, 11).Select(index => new DocumentRecord
        {
            Id = Guid.CreateVersion7(), CatalogId = catalog.Id, CreatedAt = now.AddMinutes(index),
            Data = "{\"title\":\"Book\",\"count\":2,\"price\":12.5,\"active\":true,\"tags\":[\"new\"]}"
        }).ToArray();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            context.Documents.AddRange(records);
            await context.SaveChangesAsync(token);
        }

        var first = await client.GetFromJsonAsync<GetDocumentsResponse>(route, token);
        Assert.NotNull(first);
        Assert.Equal(10, first.Items.Count);
        Assert.Equal(1, first.Page);
        Assert.Equal(10, first.PageSize);
        Assert.Equal(11, first.TotalCount);
        Assert.Equal(2, first.TotalPages);
        Assert.Equal(records.Reverse().Take(10).Select(record => record.Id), first.Items.Select(item => item.Id));
        var second = await client.GetFromJsonAsync<GetDocumentsResponse>(route + "?page=2", token);
        Assert.Equal(records[0].Id, Assert.Single(second!.Items).Id);
        Assert.Equal(2, second.Page);
        Assert.Equal(11, second.TotalCount);
        Assert.Equal(2, second.TotalPages);
        var maximum = await client.GetFromJsonAsync<GetDocumentsResponse>(route + "?pageSize=100", token);
        Assert.Equal(11, maximum!.Items.Count);
        Assert.Equal(100, maximum.PageSize);
        Assert.Equal(1, maximum.TotalPages);
        var outside = await client.GetFromJsonAsync<GetDocumentsResponse>(route + "?page=2147483647", token);
        Assert.Empty(outside!.Items);
        Assert.Equal(int.MaxValue, outside.Page);
        Assert.Equal(11, outside.TotalCount);
        Assert.Equal(2, outside.TotalPages);

        var json = await client.GetFromJsonAsync<JsonElement>(route + "?pageSize=1", token);
        Assert.Equal(5, json.EnumerateObject().Count());
        var item = Assert.Single(json.GetProperty("items").EnumerateArray());
        Assert.Equal(4, item.EnumerateObject().Count());
        Assert.Equal(records[^1].Id, item.GetProperty("id").GetGuid());
        Assert.Equal(catalog.Id, item.GetProperty("catalogId").GetGuid());
        Assert.Equal(records[^1].CreatedAt, item.GetProperty("createdAt").GetDateTimeOffset());
        Assert.Equal(TimeSpan.Zero, item.GetProperty("createdAt").GetDateTimeOffset().Offset);
        var data = item.GetProperty("data");
        Assert.Equal(JsonValueKind.Object, data.ValueKind);
        Assert.Equal("Book", data.GetProperty("title").GetString());
        Assert.Equal(2, data.GetProperty("count").GetInt32());
        Assert.Equal(12.5m, data.GetProperty("price").GetDecimal());
        Assert.True(data.GetProperty("active").GetBoolean());
        Assert.Equal("new", data.GetProperty("tags")[0].GetString());

        foreach (var (query, path, code) in new[]
        {
            ("?page=0", "page", "Document.InvalidPage"),
            ("?page=-1", "page", "Document.InvalidPage"),
            ("?page=abc", "page", "Document.InvalidPage"),
            ("?page=", "page", "Document.InvalidPage"),
            ("?page=2147483648", "page", "Document.InvalidPage"),
            ("?pageSize=0", "pageSize", "Document.InvalidPageSize"),
            ("?pageSize=-1", "pageSize", "Document.InvalidPageSize"),
            ("?pageSize=101", "pageSize", "Document.InvalidPageSize"),
            ("?pageSize=1.5", "pageSize", "Document.InvalidPageSize"),
            ("?pageSize=abc", "pageSize", "Document.InvalidPageSize"),
            ("?pageSize=", "pageSize", "Document.InvalidPageSize"),
            ("?pageSize=2147483648", "pageSize", "Document.InvalidPageSize")
        })
            await AssertProblemAsync(client, route + query, HttpStatusCode.BadRequest, code, path);

        foreach (var id in new[] { "invalid", Guid.Empty.ToString() })
            await AssertProblemAsync(client, "/api/catalog/catalogs/" + id + "/documents",
                HttpStatusCode.BadRequest, "Catalog.InvalidId", "catalogId");
        await AssertProblemAsync(client, "/api/catalog/catalogs/" + Guid.NewGuid() + "/documents",
            HttpStatusCode.NotFound, "Catalog.NotFound", "catalogId");

        var openApi = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json", token);
        var operation = openApi.GetProperty("paths").GetProperty("/api/catalog/catalogs/{catalogId}/documents").GetProperty("get");
        Assert.Equal("catalog.GetDocuments", operation.GetProperty("operationId").GetString());
        foreach (var status in new[] { "200", "400", "404" })
            Assert.True(operation.GetProperty("responses").TryGetProperty(status, out _));
        Assert.Equal(["catalogId", "page", "pageSize"], operation.GetProperty("parameters").EnumerateArray()
            .Select(parameter => parameter.GetProperty("name").GetString()));
    }

    private static async Task AssertProblemAsync(HttpClient client, string route, HttpStatusCode status, string code, string path)
    {
        var token = TestContext.Current.CancellationToken;
        using var response = await client.GetAsync(new Uri(route, UriKind.Relative), token);
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(token);
        var error = Assert.Single(problem.GetProperty("errors").EnumerateArray());
        Assert.Equal(code, error.GetProperty("code").GetString());
        Assert.Equal(path, error.GetProperty("path").GetString());
    }
}
