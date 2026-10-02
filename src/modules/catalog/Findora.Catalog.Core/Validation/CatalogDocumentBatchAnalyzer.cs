using System.Globalization;
using System.Text.Json;
using Findora.Catalog.Core.Models;
using Findora.Shared.Abstraction.Results;

namespace Findora.Catalog.Core.Validation;

public static class CatalogDocumentBatchAnalyzer
{
    public const int MaxDocuments = 100;

    public static Result<IReadOnlyList<CatalogFieldDefinition>> Analyze(
        IReadOnlyList<JsonElement> documents, Models.Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(catalog);
        if (documents.Count is < 1 or > MaxDocuments)
            return Result<IReadOnlyList<CatalogFieldDefinition>>.Failure(
                new Error("InvalidBatchSize", "A batch must contain between 1 and 100 documents.", "documents"));

        var candidate = new Models.Catalog(catalog.Id, catalog.Name);
        foreach (var field in catalog.Fields.Values) candidate.AddField(field);
        var newFields = new List<CatalogFieldDefinition>();
        var errors = new List<Error>();
        for (var index = 0; index < documents.Count; index++)
        {
            var result = CatalogDocumentAnalyzer.Analyze(documents[index], candidate);
            if (result.IsFailure)
            {
                var prefix = "documents[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                errors.AddRange(result.Errors.Select(error => new Error(error.Code, error.Message,
                    error.Path is null or "$" ? prefix : prefix + "." + error.Path)));
                continue;
            }
            foreach (var field in result.Value)
            {
                candidate.AddField(field);
                newFields.Add(field);
            }
        }
        return errors.Count > 0
            ? Result<IReadOnlyList<CatalogFieldDefinition>>.Failure(errors)
            : Result<IReadOnlyList<CatalogFieldDefinition>>.Success(newFields.AsReadOnly());
    }
}
