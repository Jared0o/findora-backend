using System.Text.Json;

namespace Findora.Catalog.Api.Contracts;

public sealed record GetDocumentResponse(Guid Id, Guid CatalogId, DateTimeOffset CreatedAt, JsonElement Data);
