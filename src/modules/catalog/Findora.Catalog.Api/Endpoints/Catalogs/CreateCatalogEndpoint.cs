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
            .WithSummary("Create a catalog.")
            .WithDescription("Creates an empty catalog and returns its identifier.");
    }

    private static async Task<Results<Created<CreateCatalogResponse>, ProblemHttpResult>> HandleAsync(
        CreateCatalogRequest request,
        CreateCatalogCommandHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(new CreateCatalogCommand(request.Name), cancellationToken);
        if (result.IsFailure)
        {
            return result.ToValidationProblem();
        }

        var location = context.Request.PathBase.Add(context.Request.Path).Value!.TrimEnd('/') + "/" + result.Value;
        return TypedResults.Created(location, new CreateCatalogResponse(result.Value));
    }
}
