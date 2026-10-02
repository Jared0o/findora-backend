using Findora.Catalog.Core.Queries.GetDocument;

namespace Findora.Catalog.Core.Queries.GetDocuments;

public sealed record DocumentPage(IReadOnlyList<DocumentDetails> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)((TotalCount + (long)PageSize - 1) / PageSize);
}
