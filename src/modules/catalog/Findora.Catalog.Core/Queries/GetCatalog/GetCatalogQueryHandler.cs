using Findora.Catalog.Core.Repository;
using Findora.Shared.Abstraction.Results;
using FluentValidation;

namespace Findora.Catalog.Core.Queries.GetCatalog;

public sealed class GetCatalogQueryHandler
{
    private readonly ICatalogRepository _repository;
    private readonly IValidator<GetCatalogQuery> _validator;

    public GetCatalogQueryHandler(ICatalogRepository repository, IValidator<GetCatalogQuery> validator)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(validator);
        _repository = repository;
        _validator = validator;
    }

    public async Task<Result<CatalogDetails>> ExecuteAsync(GetCatalogQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<CatalogDetails>.Failure(validation.Errors.Select(error =>
                new Error(error.ErrorCode, error.ErrorMessage, error.PropertyName)));
        }

        var catalog = await _repository.GetByIdAsync(Guid.Parse(query.Id), cancellationToken);
        return catalog is null
            ? Result<CatalogDetails>.Failure(new Error("Catalog.NotFound", "Catalog was not found.", "id"))
            : Result<CatalogDetails>.Success(catalog);
    }
}
