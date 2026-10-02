using System.Globalization;
using System.Text.Json;
using Findora.Catalog.Core.Models;
using Findora.Shared.Abstraction.Results;

namespace Findora.Catalog.Core.Validation;

public static class CatalogDocumentAnalyzer
{
    public static Result<IReadOnlyList<CatalogFieldDefinition>> Analyze(JsonElement document, Models.Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (document.ValueKind != JsonValueKind.Object)
            return Failure("$", "ExpectedObject", "The document must be a JSON object.");
        if (!document.EnumerateObject().Any())
            return Failure("$", "EmptyDocument", "The document must contain at least one field.");

        var candidate = new Models.Catalog(catalog.Id, catalog.Name);
        foreach (var field in catalog.Fields.Values) candidate.AddField(field);
        var newFields = new List<CatalogFieldDefinition>();
        var errors = new List<Error>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in document.EnumerateObject())
        {
            if (!seen.Add(property.Name)) continue; // The validator reports duplicate properties.
            if (string.IsNullOrWhiteSpace(property.Name) || property.Name.Contains('\0'))
            {
                errors.Add(new Error("InvalidFieldName", "Field names must be non-empty and cannot contain a null character.", property.Name));
                continue;
            }
            if (catalog.Fields.ContainsKey(property.Name)) continue;
            var type = Infer(property.Value, property.Name, errors);
            if (type is null) continue;
            var definition = new CatalogFieldDefinition(property.Name, type.Value,
                isArray: property.Value.ValueKind == JsonValueKind.Array);
            candidate.AddField(definition);
            newFields.Add(definition);
        }

        errors.AddRange(CatalogDocumentValidator.Validate(document, candidate).Errors
            .Where(error => error.Code != "UnknownField")
            .Select(error => new Error(error.Code, error.Message, error.Path)));
        return errors.Count > 0
            ? Result<IReadOnlyList<CatalogFieldDefinition>>.Failure(errors)
            : Result<IReadOnlyList<CatalogFieldDefinition>>.Success(newFields.AsReadOnly());
    }

    private static CatalogFieldType? Infer(JsonElement value, string path, List<Error> errors)
    {
        if (value.ValueKind != JsonValueKind.Array) return InferScalar(value, path, errors);
        if (value.GetArrayLength() == 0)
        {
            errors.Add(new Error("UnknownArrayType", "A new array field must contain at least one element to determine its type.", path));
            return null;
        }

        CatalogFieldType? type = null;
        var initialErrors = errors.Count;
        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            var itemPath = path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";
            var itemType = InferScalar(item, itemPath, errors);
            if (type is null) type = itemType;
            else if (itemType is not null && type != itemType)
            {
                if (IsNumeric(type.Value) && IsNumeric(itemType.Value)) type = CatalogFieldType.Decimal;
                else errors.Add(new Error("MixedArrayTypes", "Array elements must have the same type.", itemPath));
            }
            index++;
        }
        return errors.Count == initialErrors ? type : null;
    }

    private static bool IsNumeric(CatalogFieldType type) => type is CatalogFieldType.Int or CatalogFieldType.Decimal;

    private static CatalogFieldType? InferScalar(JsonElement value, string path, List<Error> errors)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String: return CatalogFieldType.String;
            case JsonValueKind.True:
            case JsonValueKind.False: return CatalogFieldType.Bool;
            case JsonValueKind.Number:
                if (value.TryGetInt32(out _)) return CatalogFieldType.Int;
                if (value.TryGetDecimal(out _)) return CatalogFieldType.Decimal;
                errors.Add(new Error("NumberOutOfRange", "The number must fit in Int32 or Decimal.", path));
                return null;
            case JsonValueKind.Null:
                errors.Add(new Error("NullNotAllowed", "Null is not allowed; omit optional fields instead.", path));
                return null;
            default:
                errors.Add(new Error("UnsupportedType", "Only strings, numbers and booleans are allowed as field or array values.", path));
                return null;
        }
    }

    private static Result<IReadOnlyList<CatalogFieldDefinition>> Failure(string path, string code, string message)
        => Result<IReadOnlyList<CatalogFieldDefinition>>.Failure(new Error(code, message, path));
}
