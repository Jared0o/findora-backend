using System.Text.Json;

namespace Findora.Catalog.Core.Queries.GetDocument;

public sealed record DocumentDetails(Guid Id, Guid CatalogId, DateTimeOffset CreatedAt, JsonElement Data);
