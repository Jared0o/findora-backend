namespace Findora.Catalog.Api.Contracts;

public sealed record GetCatalogResponse(Guid Id, string Name, DateTimeOffset CreatedAt,
    IReadOnlyList<CatalogFieldResponse> Fields);

public sealed record CatalogFieldResponse(string Name, string Type, bool IsArray, bool IsRequired);
