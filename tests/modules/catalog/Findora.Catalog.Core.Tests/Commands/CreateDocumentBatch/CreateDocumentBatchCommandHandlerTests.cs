using System.Text.Json;
using Findora.Catalog.Core.Commands.CreateDocumentBatch;
using Findora.Catalog.Core.Repository;
using Findora.Shared.Abstraction.Results;

namespace Findora.Catalog.Core.Tests.Commands.CreateDocumentBatch;

public sealed class CreateDocumentBatchCommandHandlerTests
{
    [Theory]
    [InlineData("bad", "[{}]")]
    [InlineData("00000000-0000-0000-0000-000000000000", "[{}]")]
    [InlineData("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "{}")]
    [InlineData("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "null")]
    [InlineData("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "[]")]
    public async Task InvalidEnvelope_DoesNotCallRepository(string id, string body)
    {
        using var json = JsonDocument.Parse(body);
        var repository = new RecordingRepository();
        var handler = new CreateDocumentBatchCommandHandler(repository, new CreateDocumentBatchCommandValidator());
        var result = await handler.ExecuteAsync(new(id, json.RootElement), TestContext.Current.CancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal(0, repository.Calls);
    }

    [Fact]
    public async Task ValidEnvelope_ForwardsOrderIdTokenAndRepositoryResult()
    {
        using var json = JsonDocument.Parse("[{\"index\":1},{\"index\":2}]");
        var repository = new RecordingRepository();
        var handler = new CreateDocumentBatchCommandHandler(repository, new CreateDocumentBatchCommandValidator());
        var id = Guid.NewGuid();
        var token = TestContext.Current.CancellationToken;
        var result = await handler.ExecuteAsync(new(id.ToString(), json.RootElement), token);
        Assert.Same(repository.Result, result);
        Assert.Equal(id, repository.CatalogId);
        Assert.Equal(token, repository.Token);
        Assert.Equal([1, 2], repository.Documents!.Select(document => document.GetProperty("index").GetInt32()));
    }

    [Fact]
    public async Task OversizedBatch_IsRejectedBeforeRepository()
    {
        using var json = JsonDocument.Parse("[" + string.Join(",", Enumerable.Repeat("{}", 101)) + "]");
        var repository = new RecordingRepository();
        var handler = new CreateDocumentBatchCommandHandler(repository, new CreateDocumentBatchCommandValidator());
        var result = await handler.ExecuteAsync(new(Guid.NewGuid().ToString(), json.RootElement), TestContext.Current.CancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal("InvalidBatchSize", Assert.Single(result.Errors).Code);
        Assert.Equal(0, repository.Calls);
    }

    [Fact]
    public async Task CancelledRequest_DoesNotCallRepository()
    {
        using var json = JsonDocument.Parse("[{\"x\":1}]");
        var repository = new RecordingRepository();
        var handler = new CreateDocumentBatchCommandHandler(repository, new CreateDocumentBatchCommandValidator());
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.ExecuteAsync(new(Guid.NewGuid().ToString(), json.RootElement), source.Token));
        Assert.Equal(0, repository.Calls);
    }

    private sealed class RecordingRepository : ICatalogDocumentRepository
    {
        public int Calls { get; private set; }
        public Guid CatalogId { get; private set; }
        public CancellationToken Token { get; private set; }
        public IReadOnlyList<JsonElement>? Documents { get; private set; }
        public Result<IReadOnlyList<Guid>> Result { get; } = Result<IReadOnlyList<Guid>>.Success([Guid.CreateVersion7(), Guid.CreateVersion7()]);
        public Task<Result<Guid>> CreateAsync(Guid catalogId, JsonElement document, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<IReadOnlyList<Guid>>> CreateBatchAsync(Guid catalogId, IReadOnlyList<JsonElement> documents, CancellationToken cancellationToken = default)
        {
            Calls++;
            CatalogId = catalogId;
            Token = cancellationToken;
            Documents = documents;
            return Task.FromResult(Result);
        }
    }
}
