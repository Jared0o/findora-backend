namespace Findora.Shared.Abstraction.Results;

public sealed class Result
{
    private Result(IReadOnlyList<Error> errors)
    {
        Errors = errors;
    }

    public bool IsSuccess => Errors.Count == 0;
    public bool IsFailure => !IsSuccess;
    public IReadOnlyList<Error> Errors { get; }

    public static Result Success() => new(Array.Empty<Error>());

    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Failure([error]);
    }

    public static Result Failure(IEnumerable<Error> errors) => new(ResultErrors.Copy(errors));
}
