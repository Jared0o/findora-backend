using System.Globalization;
using Findora.Catalog.Api.Contracts;
using Findora.Catalog.Core.Queries.GetCatalogs;
using Findora.Shared.Abstraction.Results;
using Findora.Shated.Infrastructure.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Findora.Catalog.Api.Endpoints.Catalogs;

internal static class GetCatalogsEndpoint
{
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/catalogs", HandleAsync)
            .WithName("catalog.GetCatalogs")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("List catalogs.")
            .WithDescription("Returns catalog metadata ordered by createdAt descending, then id descending. " +
                "Query parameters: page defaults to 1 (minimum 1); pageSize defaults to 10 (range 1–100). " +
                "Pages outside the available range return an empty items array.");
    }

    private static async Task<Results<Ok<GetCatalogsResponse>, ProblemHttpResult>> HandleAsync(
        GetCatalogsQueryHandler handler, CancellationToken cancellationToken,
        [FromQuery] string? page = null, [FromQuery] string? pageSize = null)
    {
        var errors = new List<Error>();
        var pageNumber = ParseParameter(page, 1, "page", "Catalog.InvalidPage", errors);
        var size = ParseParameter(pageSize, 10, "pageSize", "Catalog.InvalidPageSize", errors);
        if (errors.Count > 0)
        {
            return Result<CatalogPage>.Failure(errors).ToValidationProblem();
        }

        var result = await handler.ExecuteAsync(new GetCatalogsQuery(pageNumber, size), cancellationToken);
        if (result.IsFailure) return result.ToValidationProblem();
        var value = result.Value;
        return TypedResults.Ok(new GetCatalogsResponse(value.Items.Select(item =>
                new CatalogListItemResponse(item.Id, item.Name, item.CreatedAt)).ToArray(),
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
