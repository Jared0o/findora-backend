namespace Findora.Catalog.Infrastructure.Persistence.Entities;

public sealed class CatalogRecord
{
    public required Guid Id { get; init; }
    public required string Name { get; set; }
    public required DateTimeOffset CreatedAt { get; init; }
    public ICollection<CatalogFieldRecord> Fields { get; } = new List<CatalogFieldRecord>();
}
