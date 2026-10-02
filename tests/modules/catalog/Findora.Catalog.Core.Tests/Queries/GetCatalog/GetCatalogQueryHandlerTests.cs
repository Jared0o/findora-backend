using Findora.Catalog.Core.Queries.GetCatalog;
using Findora.Catalog.Core.Repository;
using Findora.Shared.Abstraction.Results;

namespace Findora.Catalog.Core.Tests.Queries.GetCatalog;

public sealed class GetCatalogQueryHandlerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bad-id")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task InvalidId_DoesNotReadRepository(string? id)
    {
        var repository = new RecordingRepository();
        var handler = new GetCatalogQueryHandler(repository, new GetCatalogQueryValidator());
        var result = await handler.ExecuteAsync(new GetCatalogQuery(id!), TestContext.Current.CancellationToken);
        Assert.True(result.IsFailure);
        var error = Assert.Single(result.Errors);
        Assert.Equal("Catalog.InvalidId", error.Code);
        Assert.Equal("id", error.Path);
        Assert.Equal(0, repository.Calls);
    }

    [Fact]
    public async Task ExistingCatalog_ReturnsDetailsAndForwardsIdAndCancellation()
    {
        var details = new CatalogDetails(Guid.CreateVersion7(), "Books", DateTimeOffset.UtcNow, []);
        var repository = new RecordingRepository { Details = details };
        var handler = new GetCatalogQueryHandler(repository, new GetCatalogQueryValidator());
        var token = TestContext.Current.CancellationToken;
        var result = await handler.ExecuteAsync(new GetCatalogQuery(details.Id.ToString()), token);
        Assert.True(result.IsSuccess);
        Assert.Same(details, result.Value);
        Assert.Equal(details.Id, repository.Id);
        Assert.Equal(token, repository.Token);
    }

    [Fact]
    public async Task MissingCatalog_ReturnsNotFound()
    {
        var handler = new GetCatalogQueryHandler(new RecordingRepository(), new GetCatalogQueryValidator());
        var result = await handler.ExecuteAsync(new GetCatalogQuery(Guid.NewGuid().ToString()), TestContext.Current.CancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal("Catalog.NotFound", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task CancelledQuery_DoesNotReadRepository()
    {
        var repository = new RecordingRepository();
        var handler = new GetCatalogQueryHandler(repository, new GetCatalogQueryValidator());
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.ExecuteAsync(new GetCatalogQuery(Guid.NewGuid().ToString()), source.Token));
        Assert.Equal(0, repository.Calls);
    }

    [Fact]
    public async Task StorageFailure_PropagatesException()
    {
        var expected = new InvalidOperationException("Database unavailable.");
        var handler = new GetCatalogQueryHandler(new RecordingRepository { Exception = expected }, new GetCatalogQueryValidator());
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.ExecuteAsync(
            new GetCatalogQuery(Guid.NewGuid().ToString()), TestContext.Current.CancellationToken));
        Assert.Same(expected, actual);
    }

    private sealed class RecordingRepository : ICatalogRepository
    {
        public CatalogDetails? Details { get; init; }
        public Exception? Exception { get; init; }
        public int Calls { get; private set; }
        public Guid Id { get; private set; }
        public CancellationToken Token { get; private set; }
        public Task<Result<Guid>> CreateAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CatalogDetails?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Calls++;
            Id = id;
            Token = cancellationToken;
            return Exception is null ? Task.FromResult(Details) : Task.FromException<CatalogDetails?>(Exception);
        }
    }
}
