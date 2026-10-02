using Findora.Shared.Abstraction.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Findora.Shated.Infrastructure.Http;

public static class ResultHttpExtensions
{
    public static ProblemHttpResult ToValidationProblem<T>(this Result<T> result) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("A successful result cannot be mapped to a validation problem.");
        }

        return TypedResults.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Validation failed.",
            extensions: new Dictionary<string, object?> { ["errors"] = result.Errors });
    }
}
