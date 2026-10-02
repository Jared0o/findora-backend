using Findora.Catalog.Api.Contracts;
using Findora.Catalog.Core.Commands.CreateCatalog;
using Findora.Shated.Infrastructure.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Findora.Catalog.Api.Endpoints.Catalogs;

internal static class CreateCatalogEndpoint
{
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/catalogs", HandleAsync)
            .WithName("catalog.CreateCatalog")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Create a catalog.")
            .WithDescription("Creates an empty catalog and returns its identifier.");
    }

    private static async Task<Results<CreatedAtRoute<CreateCatalogResponse>, ProblemHttpResult>> HandleAsync(
        CreateCatalogRequest request,
        CreateCatalogCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(new CreateCatalogCommand(request.Name), cancellationToken);
        if (result.IsFailure)
        {
            return result.Errors.Any(error => error.Code == "Catalog.NameAlreadyExists")
                ? TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Catalog name already exists.",
                    extensions: new Dictionary<string, object?> { ["errors"] = result.Errors })
                : result.ToValidationProblem();
        }

        return TypedResults.CreatedAtRoute(new CreateCatalogResponse(result.Value),
            GetCatalogEndpoint.RouteName, new { id = result.Value });
    }
}
