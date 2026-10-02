using System.Globalization;
using System.Text.Json;
using Findora.Catalog.Core.Models;

namespace Findora.Catalog.Core.Validation;

public static class CatalogProductValidator
{
    public static CatalogValidationResult Validate(JsonElement document, Models.Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var errors = new List<CatalogValidationError>();
        if (document.ValueKind != JsonValueKind.Object)
        {
            errors.Add(new("$", "ExpectedObject", "The product document must be a JSON object."));
            return new CatalogValidationResult(errors);
        }

        var suppliedFields = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in document.EnumerateObject())
        {
            if (!suppliedFields.Add(property.Name))
            {
                errors.Add(new(property.Name, "DuplicateField", "The field occurs more than once."));
                continue;
            }

            if (!catalog.Fields.TryGetValue(property.Name, out var definition))
            {
                errors.Add(new(property.Name, "UnknownField", "The field is not defined in this catalog."));
                continue;
            }

            ValidateField(property.Value, definition, errors);
        }

        foreach (var definition in catalog.Fields.Values)
        {
            if (definition.IsRequired && !suppliedFields.Contains(definition.Name))
            {
                errors.Add(new(definition.Name, "RequiredField", "The required field is missing."));
            }
        }

        return new CatalogValidationResult(errors);
    }

    private static void ValidateField(JsonElement value, CatalogFieldDefinition definition,
        List<CatalogValidationError> errors)
    {
        if (!definition.IsArray)
        {
            ValidateScalar(value, definition.Type, definition.Name, errors);
            return;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            errors.Add(new(definition.Name, "ExpectedArray", "The field must contain an array."));
            return;
        }

        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            var path = definition.Name + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";
            ValidateScalar(item, definition.Type, path, errors);
            index++;
        }
    }

    private static void ValidateScalar(JsonElement value, CatalogFieldType type, string path,
        List<CatalogValidationError> errors)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            errors.Add(new(path, "NullNotAllowed", "Null is not allowed; omit optional fields instead."));
            return;
        }

        var valid = type switch
        {
            CatalogFieldType.Int => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _),
            CatalogFieldType.Decimal => value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out _),
            CatalogFieldType.String => value.ValueKind == JsonValueKind.String,
            CatalogFieldType.Bool => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            _ => false
        };

        if (!valid)
        {
            errors.Add(new(path, "Expected" + type, $"The value must match type '{type}'."));
        }
    }
}
