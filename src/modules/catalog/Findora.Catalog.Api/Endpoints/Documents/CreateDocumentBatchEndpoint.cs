using System.Text.Json;
using Findora.Catalog.Api.Contracts;
using Findora.Catalog.Core.Commands.CreateDocumentBatch;
using Findora.Shated.Infrastructure.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Findora.Catalog.Api.Endpoints.Documents;

internal static class CreateDocumentBatchEndpoint
{
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/catalogs/{catalogId}/documents/batch", HandleAsync)
            .WithName("catalog.CreateDocumentBatch")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Create a batch of documents.")
            .WithDescription("Accepts 1–100 documents as a JSON array. Saves the entire batch and inferred fields atomically. " +
                "Earlier valid documents establish field types for later documents. Errors include zero-based document indexes. " +
                "Returned IDs follow input order.");
    }

    private static async Task<Results<Created<CreateDocumentBatchResponse>, ProblemHttpResult>> HandleAsync(
        string catalogId, [FromBody] JsonElement documents, CreateDocumentBatchCommandHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(new CreateDocumentBatchCommand(catalogId, documents), cancellationToken);
        if (result.IsFailure)
            return result.Errors.Any(error => error.Code == "Catalog.NotFound")
                ? TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Catalog not found.",
                    extensions: new Dictionary<string, object?> { ["errors"] = result.Errors })
                : result.ToValidationProblem();
        return TypedResults.Created((string?)null, new CreateDocumentBatchResponse(result.Value, result.Value.Count));
    }
}
