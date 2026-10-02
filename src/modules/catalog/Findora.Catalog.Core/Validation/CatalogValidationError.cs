namespace Findora.Catalog.Core.Validation;

public sealed record CatalogValidationError(string Path, string Code, string Message);
