namespace Findora.Catalog.Api.Contracts;

public sealed record GetDocumentsResponse(IReadOnlyList<GetDocumentResponse> Items,
    int Page, int PageSize, int TotalCount, int TotalPages);
