using Findora.Catalog.Api.Contracts;
using Findora.Catalog.Core.Models;
using Findora.Catalog.Core.Queries.GetCatalog;
using Findora.Shated.Infrastructure.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Findora.Catalog.Api.Endpoints.Catalogs;

internal static class GetCatalogEndpoint
{
    internal const string RouteName = "catalog.GetCatalog";

    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/catalogs/{id}", HandleAsync)
            .WithName(RouteName)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get a catalog.")
            .WithDescription("Returns catalog metadata and field definitions ordered by name.");
    }

    private static async Task<Results<Ok<GetCatalogResponse>, ProblemHttpResult>> HandleAsync(
        string id, GetCatalogQueryHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(new GetCatalogQuery(id), cancellationToken);
        if (result.IsFailure)
        {
            return result.Errors.Any(error => error.Code == "Catalog.NotFound")
                ? TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Catalog not found.",
                    extensions: new Dictionary<string, object?> { ["errors"] = result.Errors })
                : result.ToValidationProblem();
        }

        var catalog = result.Value;
        return TypedResults.Ok(new GetCatalogResponse(catalog.Id, catalog.Name, catalog.CreatedAt,
            catalog.Fields.Select(field => new CatalogFieldResponse(field.Name,
                ToTypeName(field.Type), field.IsArray, field.IsRequired)).ToArray()));
    }

    private static string ToTypeName(CatalogFieldType type) => type switch
    {
        CatalogFieldType.Int => "int",
        CatalogFieldType.Decimal => "decimal",
        CatalogFieldType.String => "string",
        CatalogFieldType.Bool => "bool",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported catalog field type.")
    };
}
