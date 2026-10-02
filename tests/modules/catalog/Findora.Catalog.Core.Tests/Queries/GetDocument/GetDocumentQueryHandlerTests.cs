using System.Text.Json;
using Findora.Catalog.Core.Queries.GetDocument;
using Findora.Catalog.Core.Repository;
using Findora.Shared.Abstraction.Results;

namespace Findora.Catalog.Core.Tests.Queries.GetDocument;

public sealed class GetDocumentQueryHandlerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bad-id")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task InvalidIds_CollectsBothErrorsWithoutReadingRepository(string? id)
    {
        var repository = new RecordingRepository();
        var handler = new GetDocumentQueryHandler(repository, new GetDocumentQueryValidator());
        var result = await handler.ExecuteAsync(new GetDocumentQuery(id!, id!), TestContext.Current.CancellationToken);
        Assert.True(result.IsFailure);
        Assert.Collection(result.Errors,
            error => { Assert.Equal("Catalog.InvalidId", error.Code); Assert.Equal("catalogId", error.Path); },
            error => { Assert.Equal("Document.InvalidId", error.Code); Assert.Equal("documentId", error.Path); });
        Assert.Equal(0, repository.Calls);
    }

    [Fact]
    public async Task ExistingDocument_ReturnsDetailsAndForwardsIdsAndCancellation()
    {
        using var json = JsonDocument.Parse("{\"title\":\"Book\"}");
        var details = new DocumentDetails(Guid.CreateVersion7(), Guid.CreateVersion7(), DateTimeOffset.UtcNow, json.RootElement);
        var repository = new RecordingRepository { Details = details };
        var handler = new GetDocumentQueryHandler(repository, new GetDocumentQueryValidator());
        var token = TestContext.Current.CancellationToken;
        var result = await handler.ExecuteAsync(new(details.CatalogId.ToString(), details.Id.ToString()), token);
        Assert.True(result.IsSuccess);
        Assert.Same(details, result.Value);
        Assert.Equal(details.CatalogId, repository.CatalogId);
        Assert.Equal(details.Id, repository.DocumentId);
        Assert.Equal(token, repository.Token);
    }

    [Fact]
    public async Task MissingDocument_ReturnsNotFound()
    {
        var handler = new GetDocumentQueryHandler(new RecordingRepository(), new GetDocumentQueryValidator());
        var result = await handler.ExecuteAsync(new(Guid.NewGuid().ToString(), Guid.NewGuid().ToString()), TestContext.Current.CancellationToken);
        Assert.True(result.IsFailure);
        var error = Assert.Single(result.Errors);
        Assert.Equal("Document.NotFound", error.Code);
        Assert.Equal("documentId", error.Path);
    }

    [Fact]
    public async Task CancelledQuery_DoesNotReadRepository()
    {
        var repository = new RecordingRepository();
        var handler = new GetDocumentQueryHandler(repository, new GetDocumentQueryValidator());
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.ExecuteAsync(
            new(Guid.NewGuid().ToString(), Guid.NewGuid().ToString()), source.Token));
        Assert.Equal(0, repository.Calls);
    }

    [Fact]
    public async Task StorageFailure_PropagatesException()
    {
        var expected = new InvalidOperationException("Database unavailable.");
        var handler = new GetDocumentQueryHandler(new RecordingRepository { Exception = expected }, new GetDocumentQueryValidator());
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.ExecuteAsync(
            new(Guid.NewGuid().ToString(), Guid.NewGuid().ToString()), TestContext.Current.CancellationToken));
        Assert.Same(expected, actual);
    }

    private sealed class RecordingRepository : ICatalogDocumentRepository
    {
        public DocumentDetails? Details { get; init; }
        public Exception? Exception { get; init; }
        public int Calls { get; private set; }
        public Guid CatalogId { get; private set; }
        public Guid DocumentId { get; private set; }
        public CancellationToken Token { get; private set; }

        public Task<Findora.Catalog.Core.Queries.GetDocuments.DocumentPage?> GetPageAsync(Guid catalogId, int page, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DocumentDetails?> GetByIdAsync(Guid catalogId, Guid documentId, CancellationToken cancellationToken = default)
        {
            Calls++;
            CatalogId = catalogId;
            DocumentId = documentId;
            Token = cancellationToken;
            return Exception is null ? Task.FromResult(Details) : Task.FromException<DocumentDetails?>(Exception);
        }

        public Task<Result<Guid>> CreateAsync(Guid catalogId, JsonElement document, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<Result<IReadOnlyList<Guid>>> CreateBatchAsync(Guid catalogId, IReadOnlyList<JsonElement> documents, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
