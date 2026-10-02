using Findora.Catalog.Core.Repository;
using Findora.Shared.Abstraction.Results;
using FluentValidation;

namespace Findora.Catalog.Core.Queries.GetDocuments;

public sealed class GetDocumentsQueryHandler
{
    private readonly ICatalogDocumentRepository _repository;
    private readonly IValidator<GetDocumentsQuery> _validator;

    public GetDocumentsQueryHandler(ICatalogDocumentRepository repository, IValidator<GetDocumentsQuery> validator)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(validator);
        _repository = repository;
        _validator = validator;
    }

    public async Task<Result<DocumentPage>> ExecuteAsync(GetDocumentsQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
            return Result<DocumentPage>.Failure(validation.Errors.Select(error =>
                new Error(error.ErrorCode, error.ErrorMessage, error.PropertyName)));

        var page = await _repository.GetPageAsync(Guid.Parse(query.CatalogId), query.Page, query.PageSize, cancellationToken);
        return page is null
            ? Result<DocumentPage>.Failure(new Error("Catalog.NotFound", "Catalog was not found.", "catalogId"))
            : Result<DocumentPage>.Success(page);
    }
}
