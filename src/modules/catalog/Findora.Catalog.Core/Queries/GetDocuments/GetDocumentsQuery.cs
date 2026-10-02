namespace Findora.Catalog.Core.Queries.GetDocuments;

public sealed record GetDocumentsQuery(string CatalogId, int Page = 1, int PageSize = 10);
