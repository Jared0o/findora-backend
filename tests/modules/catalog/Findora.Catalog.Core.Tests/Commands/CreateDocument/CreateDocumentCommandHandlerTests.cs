using System.Text.Json;
using Findora.Catalog.Core.Commands.CreateDocument;
using Findora.Catalog.Core.Repository;
using Findora.Shared.Abstraction.Results;

namespace Findora.Catalog.Core.Tests.Commands.CreateDocument;

public sealed class CreateDocumentCommandHandlerTests
{
    [Theory]
    [InlineData("bad", "{\"x\":1}")]
    [InlineData("00000000-0000-0000-0000-000000000000", "{\"x\":1}")]
    [InlineData("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "{}")]
    [InlineData("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "[]")]
    public async Task InvalidInput_DoesNotCallRepository(string id, string json)
    {
        using var document = JsonDocument.Parse(json);
        var repository = new RecordingRepository();
        var handler = new CreateDocumentCommandHandler(repository, new CreateDocumentCommandValidator());
        var result = await handler.ExecuteAsync(new(id, document.RootElement), TestContext.Current.CancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal(0, repository.Calls);
    }

    [Fact]
    public async Task ValidInput_ForwardsDocumentIdTokenAndResult()
    {
        using var document = JsonDocument.Parse("{\"x\":1}");
        var repository = new RecordingRepository();
        var handler = new CreateDocumentCommandHandler(repository, new CreateDocumentCommandValidator());
        var id = Guid.NewGuid();
        var token = TestContext.Current.CancellationToken;
        var result = await handler.ExecuteAsync(new(id.ToString(), document.RootElement), token);
        Assert.Same(repository.Result, result);
        Assert.Equal(id, repository.CatalogId);
        Assert.Equal(document.RootElement, repository.Document);
        Assert.Equal(token, repository.Token);
    }

    [Fact]
    public async Task CancelledRequest_DoesNotCallRepository()
    {
        using var document = JsonDocument.Parse("{\"x\":1}");
        var repository = new RecordingRepository();
        var handler = new CreateDocumentCommandHandler(repository, new CreateDocumentCommandValidator());
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.ExecuteAsync(new(Guid.NewGuid().ToString(), document.RootElement), source.Token));
        Assert.Equal(0, repository.Calls);
    }

    private sealed class RecordingRepository : ICatalogDocumentRepository
    {
        public Task<Findora.Catalog.Core.Queries.GetDocuments.DocumentPage?> GetPageAsync(Guid catalogId, int page, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Findora.Catalog.Core.Queries.GetDocument.DocumentDetails?> GetByIdAsync(Guid catalogId, Guid documentId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Result<IReadOnlyList<Guid>>> CreateBatchAsync(Guid catalogId, IReadOnlyList<System.Text.Json.JsonElement> documents, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public int Calls { get; private set; }
        public Guid CatalogId { get; private set; }
        public JsonElement Document { get; private set; }
        public CancellationToken Token { get; private set; }
        public Result<Guid> Result { get; } = Result<Guid>.Failure(new Error("Catalog.NotFound", "Missing catalog.", "catalogId"));
        public Task<Result<Guid>> CreateAsync(Guid catalogId, JsonElement document, CancellationToken cancellationToken = default)
        {
            Calls++;
            CatalogId = catalogId;
            Document = document;
            Token = cancellationToken;
            return Task.FromResult(Result);
        }
    }
}
