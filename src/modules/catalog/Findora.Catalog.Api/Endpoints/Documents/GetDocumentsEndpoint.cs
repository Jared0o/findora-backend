using System.Globalization;
using Findora.Catalog.Api.Contracts;
using Findora.Catalog.Core.Queries.GetDocuments;
using Findora.Shared.Abstraction.Results;
using Findora.Shated.Infrastructure.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Findora.Catalog.Api.Endpoints.Documents;

internal static class GetDocumentsEndpoint
{
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/catalogs/{catalogId}/documents", HandleAsync)
            .WithName("catalog.GetDocuments")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("List documents.")
            .WithDescription("Returns full documents from the specified catalog ordered by createdAt descending, then id descending. " +
                "Query parameters: page defaults to 1 (minimum 1); pageSize defaults to 10 (range 1–100). " +
                "Pages outside the available range return an empty items array. A missing catalog returns 404.");
    }

    private static async Task<Results<Ok<GetDocumentsResponse>, ProblemHttpResult>> HandleAsync(
        string catalogId, GetDocumentsQueryHandler handler, CancellationToken cancellationToken,
        [FromQuery] string? page = null, [FromQuery] string? pageSize = null)
    {
        var errors = new List<Error>();
        var pageNumber = ParseParameter(page, 1, "page", "Document.InvalidPage", errors);
        var size = ParseParameter(pageSize, 10, "pageSize", "Document.InvalidPageSize", errors);
        if (errors.Count > 0) return Result<DocumentPage>.Failure(errors).ToValidationProblem();

        var result = await handler.ExecuteAsync(new GetDocumentsQuery(catalogId, pageNumber, size), cancellationToken);
        if (result.IsFailure)
            return result.Errors.Any(error => error.Code == "Catalog.NotFound")
                ? TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Catalog not found.",
                    extensions: new Dictionary<string, object?> { ["errors"] = result.Errors })
                : result.ToValidationProblem();

        var value = result.Value;
        return TypedResults.Ok(new GetDocumentsResponse(value.Items.Select(item =>
                new GetDocumentResponse(item.Id, item.CatalogId, item.CreatedAt, item.Data)).ToArray(),
            value.Page, value.PageSize, value.TotalCount, value.TotalPages));
    }

    private static int ParseParameter(string? value, int defaultValue, string path, string code, List<Error> errors)
    {
        if (value is null) return defaultValue;
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        errors.Add(new Error(code, "The value must be a 32-bit integer.", path));
        return defaultValue;
    }
}
