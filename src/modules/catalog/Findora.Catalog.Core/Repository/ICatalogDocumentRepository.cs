using System.Text.Json;
using Findora.Shared.Abstraction.Results;

namespace Findora.Catalog.Core.Repository;

public interface ICatalogDocumentRepository
{
    Task<Result<Guid>> CreateAsync(Guid catalogId, JsonElement document, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<Guid>>> CreateBatchAsync(Guid catalogId, IReadOnlyList<JsonElement> documents, CancellationToken cancellationToken = default);
}
