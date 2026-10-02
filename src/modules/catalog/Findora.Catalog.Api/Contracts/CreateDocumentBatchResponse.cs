namespace Findora.Catalog.Api.Contracts;

public sealed record CreateDocumentBatchResponse(IReadOnlyList<Guid> Ids, int CreatedCount);
