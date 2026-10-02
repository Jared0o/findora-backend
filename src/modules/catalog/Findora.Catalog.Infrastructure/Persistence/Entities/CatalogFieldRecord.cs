using Findora.Catalog.Core.Models;

namespace Findora.Catalog.Infrastructure.Persistence.Entities;

public sealed class CatalogFieldRecord
{
    public required Guid CatalogId { get; init; }
    public required string Name { get; init; }
    public required CatalogFieldType Type { get; init; }
    public bool IsArray { get; init; }
    public bool IsRequired { get; init; }
}
