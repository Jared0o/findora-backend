using Findora.Catalog.Api.Contracts;
using Findora.Catalog.Core.Queries.GetDocument;
using Findora.Shated.Infrastructure.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Findora.Catalog.Api.Endpoints.Documents;

internal static class GetDocumentEndpoint
{
    internal const string RouteName = "catalog.GetDocument";

    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/catalogs/{catalogId}/documents/{documentId}", HandleAsync)
            .WithName(RouteName)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get a document.")
            .WithDescription("Returns document metadata and data as a JSON object. The document must belong to the specified catalog.");
    }

    private static async Task<Results<Ok<GetDocumentResponse>, ProblemHttpResult>> HandleAsync(
        string catalogId, string documentId, GetDocumentQueryHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(new GetDocumentQuery(catalogId, documentId), cancellationToken);
        if (result.IsFailure)
            return result.Errors.Any(error => error.Code == "Document.NotFound")
                ? TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Document not found.",
                    extensions: new Dictionary<string, object?> { ["errors"] = result.Errors })
                : result.ToValidationProblem();

        var document = result.Value;
        return TypedResults.Ok(new GetDocumentResponse(document.Id, document.CatalogId, document.CreatedAt, document.Data));
    }
}
