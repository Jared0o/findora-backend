using System.Text.Json;
using Findora.Catalog.Api.Contracts;
using Findora.Catalog.Core.Commands.CreateDocument;
using Findora.Shated.Infrastructure.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Findora.Catalog.Api.Endpoints.Documents;

internal static class CreateDocumentEndpoint
{
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/catalogs/{catalogId}/documents", HandleAsync)
            .WithName("catalog.CreateDocument")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Create a document.")
            .WithDescription("Accepts a non-empty flat JSON object. New fields are inferred as optional; existing types remain fixed. " +
                "Supports strings, booleans, Int32, Decimal and homogeneous arrays. New empty arrays, null and nested values are rejected. " +
                "The document and discovered field definitions are saved atomically.");
    }

    private static async Task<Results<Created<CreateDocumentResponse>, ProblemHttpResult>> HandleAsync(
        string catalogId, [FromBody] JsonElement document, CreateDocumentCommandHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(new CreateDocumentCommand(catalogId, document), cancellationToken);
        if (result.IsFailure)
            return result.Errors.Any(error => error.Code == "Catalog.NotFound")
                ? TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Catalog not found.",
                    extensions: new Dictionary<string, object?> { ["errors"] = result.Errors })
                : result.ToValidationProblem();
        return TypedResults.Created((string?)null, new CreateDocumentResponse(result.Value));
    }
}
