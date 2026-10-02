using Findora.Catalog.Core.Queries.GetCatalog;
using Findora.Catalog.Core.Queries.GetCatalogs;
using Findora.Catalog.Core.Repository;
using Findora.Shared.Abstraction.Results;

namespace Findora.Catalog.Core.Tests.Queries.GetCatalogs;

public sealed class GetCatalogsQueryHandlerTests
{
    [Theory]
    [InlineData(0, 10, "page")]
    [InlineData(-1, 10, "page")]
    [InlineData(1, 0, "pageSize")]
    [InlineData(1, -1, "pageSize")]
    [InlineData(1, 101, "pageSize")]
    public async Task InvalidParameters_AreRejectedBeforeReading(int page, int size, string path)
    {
        var repository = new RecordingRepository();
        var handler = new GetCatalogsQueryHandler(repository, new GetCatalogsQueryValidator());
        var result = await handler.ExecuteAsync(new GetCatalogsQuery(page, size), TestContext.Current.CancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal(path, Assert.Single(result.Errors).Path);
        Assert.Equal(0, repository.Calls);
    }

    [Fact]
    public async Task Validation_CollectsBothErrors()
    {
        var result = await new GetCatalogsQueryValidator().ValidateAsync(new GetCatalogsQuery(0, 101), TestContext.Current.CancellationToken);
        Assert.Equal(["page", "pageSize"], result.Errors.Select(error => error.PropertyName));
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 100)]
    [InlineData(int.MaxValue, 100)]
    public async Task ValidParameters_AreForwardedWithCancellation(int page, int size)
    {
        var repository = new RecordingRepository();
        var handler = new GetCatalogsQueryHandler(repository, new GetCatalogsQueryValidator());
        var token = TestContext.Current.CancellationToken;
        var result = await handler.ExecuteAsync(new GetCatalogsQuery(page, size), token);
        Assert.True(result.IsSuccess);
        Assert.Equal(page, result.Value.Page);
        Assert.Equal(size, result.Value.PageSize);
        Assert.Equal(21, result.Value.TotalCount);
        Assert.Equal(size == 10 ? 3 : 1, result.Value.TotalPages);
        Assert.Equal(token, repository.Token);
    }

    [Fact]
    public async Task Cancellation_PreventsReading()
    {
        var repository = new RecordingRepository();
        var handler = new GetCatalogsQueryHandler(repository, new GetCatalogsQueryValidator());
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.ExecuteAsync(new GetCatalogsQuery(), source.Token));
        Assert.Equal(0, repository.Calls);
    }

    private sealed class RecordingRepository : ICatalogRepository
    {
        public int Calls { get; private set; }
        public CancellationToken Token { get; private set; }
        public Task<CatalogPage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken = default)
        {
            Calls++;
            Token = cancellationToken;
            return Task.FromResult(new CatalogPage([], page, pageSize, 21));
        }
        public Task<CatalogDetails?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<Guid>> CreateAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
