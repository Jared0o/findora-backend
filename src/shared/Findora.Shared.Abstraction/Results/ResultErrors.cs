namespace Findora.Shared.Abstraction.Results;

internal static class ResultErrors
{
    internal static IReadOnlyList<Error> Copy(IEnumerable<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        var copy = errors.ToArray();

        if (copy.Length == 0)
        {
            throw new ArgumentException("A failed result must contain at least one error.", nameof(errors));
        }

        if (Array.Exists(copy, error => error is null))
        {
            throw new ArgumentException("Errors must not contain null elements.", nameof(errors));
        }

        return Array.AsReadOnly(copy);
    }
}
