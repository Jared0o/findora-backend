namespace Findora.Shared.Abstraction.Results;

public sealed class Result<T> where T : notnull
{
    private readonly T? _value;

    private Result(T? value, IReadOnlyList<Error> errors)
    {
        _value = value;
        Errors = errors;
    }

    public bool IsSuccess => Errors.Count == 0;
    public bool IsFailure => !IsSuccess;
    public IReadOnlyList<Error> Errors { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("A failed result has no value.");

    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Result<T>(value, Array.Empty<Error>());
    }

    public static Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Failure([error]);
    }

    public static Result<T> Failure(IEnumerable<Error> errors) => new(default, ResultErrors.Copy(errors));
}
