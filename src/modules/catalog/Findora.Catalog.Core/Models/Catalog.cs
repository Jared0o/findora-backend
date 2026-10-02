using System.Collections.ObjectModel;

namespace Findora.Catalog.Core.Models;

public sealed class Catalog
{
    private readonly Dictionary<string, CatalogFieldDefinition> _fields = new(StringComparer.Ordinal);

    public Catalog(Guid id, string name)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Catalog ID must not be empty.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Id = id;
        Name = name;
        Fields = new ReadOnlyDictionary<string, CatalogFieldDefinition>(_fields);
    }

    public Guid Id { get; }
    public string Name { get; }
    public IReadOnlyDictionary<string, CatalogFieldDefinition> Fields { get; }

    public void AddField(CatalogFieldDefinition field)
    {
        ArgumentNullException.ThrowIfNull(field);
        if (!_fields.TryAdd(field.Name, field))
        {
            throw new InvalidOperationException($"Field '{field.Name}' already exists in this catalog.");
        }
    }
}
