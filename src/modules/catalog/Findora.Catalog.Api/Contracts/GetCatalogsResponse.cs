namespace Findora.Catalog.Api.Contracts;

public sealed record CatalogListItemResponse(Guid Id, string Name, DateTimeOffset CreatedAt);

public sealed record GetCatalogsResponse(IReadOnlyList<CatalogListItemResponse> Items,
    int Page, int PageSize, int TotalCount, int TotalPages);
