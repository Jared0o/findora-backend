using System.Text.Json;
using Findora.Catalog.Core.Queries.GetDocument;
using Findora.Catalog.Core.Queries.GetDocuments;
using Findora.Catalog.Core.Repository;
using Findora.Shared.Abstraction.Results;

namespace Findora.Catalog.Core.Tests.Queries.GetDocuments;

public sealed class GetDocumentsQueryHandlerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bad-id")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task InvalidCatalogId_DoesNotReadRepository(string? id)
    {
        var repository = new RecordingRepository();
        var handler = new GetDocumentsQueryHandler(repository, new GetDocumentsQueryValidator());
        var result = await handler.ExecuteAsync(new(id!), TestContext.Current.CancellationToken);
        Assert.True(result.IsFailure);
        var error = Assert.Single(result.Errors);
        Assert.Equal("Catalog.InvalidId", error.Code);
        Assert.Equal("catalogId", error.Path);
        Assert.Equal(0, repository.Calls);
    }

    [Theory]
    [InlineData(0, 10, "page", "Document.InvalidPage")]
    [InlineData(-1, 10, "page", "Document.InvalidPage")]
    [InlineData(1, 0, "pageSize", "Document.InvalidPageSize")]
    [InlineData(1, -1, "pageSize", "Document.InvalidPageSize")]
    [InlineData(1, 101, "pageSize", "Document.InvalidPageSize")]
    public async Task InvalidPagination_DoesNotReadRepository(int page, int size, string path, string code)
    {
        var repository = new RecordingRepository();
        var handler = new GetDocumentsQueryHandler(repository, new GetDocumentsQueryValidator());
        var result = await handler.ExecuteAsync(new(Guid.NewGuid().ToString(), page, size), TestContext.Current.CancellationToken);
        Assert.True(result.IsFailure);
        var error = Assert.Single(result.Errors);
        Assert.Equal(path, error.Path);
        Assert.Equal(code, error.Code);
        Assert.Equal(0, repository.Calls);
    }

    [Fact]
    public async Task Validation_CollectsAllErrors()
    {
        var result = await new GetDocumentsQueryValidator().ValidateAsync(new GetDocumentsQuery("bad", 0, 101), TestContext.Current.CancellationToken);
        Assert.Equal(["catalogId", "page", "pageSize"], result.Errors.Select(error => error.PropertyName));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 10)]
    [InlineData(2, 100)]
    [InlineData(int.MaxValue, 100)]
    public async Task ValidParameters_ForwardCatalogPageSizeAndCancellation(int page, int size)
    {
        var id = Guid.NewGuid();
        var repository = new RecordingRepository { Result = new DocumentPage([], page, size, 21) };
        var handler = new GetDocumentsQueryHandler(repository, new GetDocumentsQueryValidator());
        var token = TestContext.Current.CancellationToken;
        var result = await handler.ExecuteAsync(new(id.ToString(), page, size), token);
        Assert.True(result.IsSuccess);
        Assert.Same(repository.Result, result.Value);
        Assert.Equal((id, page, size), repository.Parameters);
        Assert.Equal(token, repository.Token);
    }

    [Fact]
    public async Task DefaultsAndMissingCatalog_ReturnNotFound()
    {
        var id = Guid.NewGuid();
        var repository = new RecordingRepository();
        var handler = new GetDocumentsQueryHandler(repository, new GetDocumentsQueryValidator());
        var result = await handler.ExecuteAsync(new(id.ToString()), TestContext.Current.CancellationToken);
        Assert.Equal((id, 1, 10), repository.Parameters);
        Assert.True(result.IsFailure);
        var error = Assert.Single(result.Errors);
        Assert.Equal("Catalog.NotFound", error.Code);
        Assert.Equal("catalogId", error.Path);
    }

    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(20, 10, 2)]
    [InlineData(21, 10, 3)]
    [InlineData(int.MaxValue, 100, 21474837)]
    public void TotalPages_RoundsUpWithoutOverflow(int count, int size, int expected)
        => Assert.Equal(expected, new DocumentPage([], 1, size, count).TotalPages);

    [Fact]
    public async Task Cancellation_PreventsReading()
    {
        var repository = new RecordingRepository();
        var handler = new GetDocumentsQueryHandler(repository, new GetDocumentsQueryValidator());
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.ExecuteAsync(new(Guid.NewGuid().ToString()), source.Token));
        Assert.Equal(0, repository.Calls);
    }

    [Fact]
    public async Task StorageFailure_PropagatesException()
    {
        var expected = new InvalidOperationException("Database unavailable.");
        var handler = new GetDocumentsQueryHandler(new RecordingRepository { Exception = expected }, new GetDocumentsQueryValidator());
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.ExecuteAsync(
            new(Guid.NewGuid().ToString()), TestContext.Current.CancellationToken));
        Assert.Same(expected, actual);
    }

    private sealed class RecordingRepository : ICatalogDocumentRepository
    {
        public DocumentPage? Result { get; init; }
        public Exception? Exception { get; init; }
        public int Calls { get; private set; }
        public (Guid CatalogId, int Page, int PageSize) Parameters { get; private set; }
        public CancellationToken Token { get; private set; }
        public Task<DocumentPage?> GetPageAsync(Guid catalogId, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            Calls++;
            Parameters = (catalogId, page, pageSize);
            Token = cancellationToken;
            return Exception is null ? Task.FromResult(Result) : Task.FromException<DocumentPage?>(Exception);
        }

        public Task<DocumentDetails?> GetByIdAsync(Guid catalogId, Guid documentId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<Result<Guid>> CreateAsync(Guid catalogId, JsonElement document, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<Result<IReadOnlyList<Guid>>> CreateBatchAsync(Guid catalogId, IReadOnlyList<JsonElement> documents, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
