using Findora.Catalog.Core.Models;

namespace Findora.Catalog.Core.Queries.GetCatalog;

public sealed record CatalogDetails(Guid Id, string Name, DateTimeOffset CreatedAt,
    IReadOnlyList<CatalogFieldDetails> Fields);

public sealed record CatalogFieldDetails(string Name, CatalogFieldType Type, bool IsArray, bool IsRequired);
