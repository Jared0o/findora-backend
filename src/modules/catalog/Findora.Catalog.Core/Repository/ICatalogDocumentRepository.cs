using System.Text.Json;
using Findora.Shared.Abstraction.Results;
using Findora.Catalog.Core.Queries.GetDocument;
using Findora.Catalog.Core.Queries.GetDocuments;

namespace Findora.Catalog.Core.Repository;

public interface ICatalogDocumentRepository
{
    Task<DocumentPage?> GetPageAsync(Guid catalogId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<DocumentDetails?> GetByIdAsync(Guid catalogId, Guid documentId, CancellationToken cancellationToken = default);
    Task<Result<Guid>> CreateAsync(Guid catalogId, JsonElement document, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<Guid>>> CreateBatchAsync(Guid catalogId, IReadOnlyList<JsonElement> documents, CancellationToken cancellationToken = default);
}
