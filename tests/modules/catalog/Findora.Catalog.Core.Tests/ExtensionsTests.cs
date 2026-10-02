using Findora.Catalog.Core.Commands.CreateCatalog;
using Findora.Catalog.Core.Repository;
using Findora.Shared.Abstraction.Results;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Findora.Catalog.Core.Tests;

public sealed class ExtensionsTests
{
    [Fact]
    public void AddCatalogCore_RejectsNullServices()
    {
        Assert.Throws<ArgumentNullException>(() => Extensions.AddCatalogCore(null!));
    }

    [Fact]
    public void AddCatalogCore_RegistersCommandValidator()
    {
        var services = new ServiceCollection();

        var returned = services.AddCatalogCore();

        Assert.Same(services, returned);
        var registration = Assert.Single(services,
            descriptor => descriptor.ServiceType == typeof(IValidator<CreateCatalogCommand>));
        Assert.Equal(typeof(CreateCatalogCommandValidator), registration.ImplementationType);
        Assert.Equal(ServiceLifetime.Transient, registration.Lifetime);
    }

    [Fact]
    public async Task AddCatalogCore_ResolvesHandlerAndValidatesBeforeWriting()
    {
        var services = new ServiceCollection();
        services.AddCatalogCore();
        services.AddScoped<ICatalogRepository, RecordingCatalogRepository>();
        services.AddScoped<ICatalogDocumentRepository, RecordingCatalogRepository>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<CreateCatalogCommandHandler>();
        var repository = Assert.IsType<RecordingCatalogRepository>(
            scope.ServiceProvider.GetRequiredService<ICatalogRepository>());

        var invalid = await handler.ExecuteAsync(new CreateCatalogCommand(" "),
            TestContext.Current.CancellationToken);

        Assert.True(invalid.IsFailure);
        Assert.Equal("Catalog.NameRequired", Assert.Single(invalid.Errors).Code);
        Assert.Equal(0, repository.Calls);

        var valid = await handler.ExecuteAsync(new CreateCatalogCommand(" Products "),
            TestContext.Current.CancellationToken);

        Assert.True(valid.IsSuccess);
        Assert.Equal(1, repository.Calls);
        Assert.Equal("Products", repository.Name);
        Assert.Same(handler, scope.ServiceProvider.GetRequiredService<CreateCatalogCommandHandler>());
    }

    private sealed class RecordingCatalogRepository : ICatalogRepository, ICatalogDocumentRepository
    {
        public Task<Result<IReadOnlyList<Guid>>> CreateBatchAsync(Guid catalogId, IReadOnlyList<System.Text.Json.JsonElement> documents, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result<Guid>> CreateAsync(Guid catalogId, System.Text.Json.JsonElement document, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<Findora.Catalog.Core.Queries.GetCatalogs.CatalogPage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Findora.Catalog.Core.Queries.GetCatalog.CatalogDetails?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public int Calls { get; private set; }
        public string? Name { get; private set; }

        public Task<Result<Guid>> CreateAsync(string name, CancellationToken cancellationToken = default)
        {
            Calls++;
            Name = name;
            return Task.FromResult(Result<Guid>.Success(Guid.CreateVersion7()));
        }
    }
}
