namespace Findora.Catalog.Core.Models;

public sealed record CatalogFieldDefinition
{
    public CatalogFieldDefinition(string name, CatalogFieldType type, bool isArray = false, bool isRequired = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        Name = name;
        Type = type;
        IsArray = isArray;
        IsRequired = isRequired;
    }

    public string Name { get; }
    public CatalogFieldType Type { get; }
    public bool IsArray { get; }
    public bool IsRequired { get; }
}
