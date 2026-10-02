namespace Findora.Catalog.Core.Queries.GetCatalogs;

public sealed record CatalogListItem(Guid Id, string Name, DateTimeOffset CreatedAt);

public sealed record CatalogPage(IReadOnlyList<CatalogListItem> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)((TotalCount + (long)PageSize - 1) / PageSize);
}
