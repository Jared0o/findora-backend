namespace Findora.Catalog.Core.Validation;

public sealed class CatalogValidationResult
{
    internal CatalogValidationResult(IEnumerable<CatalogValidationError> errors)
    {
        Errors = Array.AsReadOnly(errors.ToArray());
    }

    public bool IsValid => Errors.Count == 0;
    public IReadOnlyList<CatalogValidationError> Errors { get; }
}
