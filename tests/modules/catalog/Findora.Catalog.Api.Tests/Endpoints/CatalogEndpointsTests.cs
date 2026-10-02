using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Findora.Catalog.Api;
using Findora.Catalog.Api.Contracts;
using Findora.Catalog.Core.Repository;
using Findora.Shared.Abstraction.Results;
using Findora.Shated.Infrastructure.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Findora.Catalog.Api.Tests.Endpoints;

public sealed class CatalogEndpointsTests
{
    [Theory]
    [InlineData("/api/catalog")]
    [InlineData("/api/catalog/")]
    public async Task GetRoot_IntroducesModule(string path)
    {
        var repository = new RecordingRepository();
        await using var app = await StartAsync(repository);
        using var client = app.GetTestClient();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("catalog", body.GetProperty("name").GetString());
        Assert.Equal(0, repository.Calls);
    }

    [Fact]
    public async Task Create_ReturnsIdentifierAndLocation()
    {
        var repository = new RecordingRepository();
        await using var app = await StartAsync(repository);
        using var client = app.GetTestClient();

        using var response = await client.PostAsJsonAsync("/api/catalog/catalogs", new CreateCatalogRequest("  Books  "), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<CreateCatalogResponse>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(repository.Id, body!.Id);
        Assert.Equal("/api/catalog/catalogs/" + repository.Id, response.Headers.Location!.OriginalString);
        Assert.Equal("Books", repository.Name);
        Assert.Equal(1, repository.Calls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Create_InvalidNameReturnsProblemWithoutSaving(string? name)
    {
        var repository = new RecordingRepository();
        await using var app = await StartAsync(repository);
        using var client = app.GetTestClient();

        using var response = await client.PostAsJsonAsync("/api/catalog/catalogs", new { name }, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(400, body.GetProperty("status").GetInt32());
        var error = Assert.Single(body.GetProperty("errors").EnumerateArray());
        Assert.Equal("Catalog.NameRequired", error.GetProperty("code").GetString());
        Assert.Equal("name", error.GetProperty("path").GetString());
        Assert.Equal(0, repository.Calls);
    }

    [Fact]
    public async Task Create_PreservesAllFailureDetails()
    {
        var repository = new RecordingRepository
        {
            Failure = Result<Guid>.Failure([
                new Error("Catalog.First", "First error.", "name"),
                new Error("Catalog.Second", "Second error.", "fields")])
        };
        await using var app = await StartAsync(repository);
        using var client = app.GetTestClient();

        using var response = await client.PostAsJsonAsync("/api/catalog/catalogs", new CreateCatalogRequest("Books"), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = body.GetProperty("errors").EnumerateArray().ToArray();
        Assert.Equal(2, errors.Length);
        Assert.Equal("Catalog.First", errors[0].GetProperty("code").GetString());
        Assert.Equal("Second error.", errors[1].GetProperty("message").GetString());
        Assert.Equal("fields", errors[1].GetProperty("path").GetString());
    }

    [Fact]
    public async Task OpenApi_DescribesGroupedModuleAndCreateResponses()
    {
        await using var app = await StartAsync(new RecordingRepository());
        using var client = app.GetTestClient();

        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json", TestContext.Current.CancellationToken);
        var paths = document.GetProperty("paths");
        Assert.Equal("catalog.Info", paths.GetProperty("/api/catalog").GetProperty("get").GetProperty("operationId").GetString());
        var operation = paths.GetProperty("/api/catalog/catalogs").GetProperty("post");
        Assert.Equal("catalog", Assert.Single(operation.GetProperty("tags").EnumerateArray()).GetString());
        Assert.True(operation.GetProperty("responses").TryGetProperty("201", out _));
        Assert.True(operation.GetProperty("responses").TryGetProperty("400", out _));
    }

    private static async Task<WebApplication> StartAsync(RecordingRepository repository)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Findora"] = "Host=localhost;Database=unused;Username=unused;Password=unused"
        });
        builder.Services.AddOpenApi();
        builder.Services.AddModule<CatalogModule>(builder.Configuration);
        builder.Services.RemoveAll<ICatalogRepository>();
        builder.Services.AddSingleton<ICatalogRepository>(repository);
        var app = builder.Build();
        app.MapModules();
        app.MapOpenApi();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private sealed class RecordingRepository : ICatalogRepository
    {
        public Guid Id { get; } = Guid.CreateVersion7();
        public int Calls { get; private set; }
        public string? Name { get; private set; }
        public Result<Guid>? Failure { get; init; }

        public Task<Result<Guid>> CreateAsync(string name, CancellationToken cancellationToken = default)
        {
            Calls++;
            Name = name;
            return Task.FromResult(Failure ?? Result<Guid>.Success(Id));
        }
    }
}
