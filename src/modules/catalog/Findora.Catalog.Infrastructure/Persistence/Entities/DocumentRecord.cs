namespace Findora.Catalog.Infrastructure.Persistence.Entities;

public sealed class DocumentRecord
{
    public required Guid Id { get; init; }
    public required Guid CatalogId { get; init; }
    public required string Data { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
