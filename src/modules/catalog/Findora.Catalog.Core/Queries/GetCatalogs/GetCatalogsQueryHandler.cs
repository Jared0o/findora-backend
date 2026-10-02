using Findora.Catalog.Core.Repository;
using Findora.Shared.Abstraction.Results;
using FluentValidation;

namespace Findora.Catalog.Core.Queries.GetCatalogs;

public sealed class GetCatalogsQueryHandler
{
    private readonly ICatalogRepository _repository;
    private readonly IValidator<GetCatalogsQuery> _validator;

    public GetCatalogsQueryHandler(ICatalogRepository repository, IValidator<GetCatalogsQuery> validator)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(validator);
        _repository = repository;
        _validator = validator;
    }

    public async Task<Result<CatalogPage>> ExecuteAsync(GetCatalogsQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<CatalogPage>.Failure(validation.Errors.Select(error =>
                new Error(error.ErrorCode, error.ErrorMessage, error.PropertyName)));
        }

        return Result<CatalogPage>.Success(await _repository.GetPageAsync(query.Page, query.PageSize, cancellationToken));
    }
}
