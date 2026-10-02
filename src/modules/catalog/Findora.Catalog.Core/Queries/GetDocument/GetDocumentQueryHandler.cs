using Findora.Catalog.Core.Repository;
using Findora.Shared.Abstraction.Results;
using FluentValidation;

namespace Findora.Catalog.Core.Queries.GetDocument;

public sealed class GetDocumentQueryHandler
{
    private readonly ICatalogDocumentRepository _repository;
    private readonly IValidator<GetDocumentQuery> _validator;

    public GetDocumentQueryHandler(ICatalogDocumentRepository repository, IValidator<GetDocumentQuery> validator)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(validator);
        _repository = repository;
        _validator = validator;
    }

    public async Task<Result<DocumentDetails>> ExecuteAsync(GetDocumentQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
            return Result<DocumentDetails>.Failure(validation.Errors.Select(error =>
                new Error(error.ErrorCode, error.ErrorMessage, error.PropertyName)));

        var document = await _repository.GetByIdAsync(Guid.Parse(query.CatalogId), Guid.Parse(query.DocumentId), cancellationToken);
        return document is null
            ? Result<DocumentDetails>.Failure(new Error("Document.NotFound", "Document was not found.", "documentId"))
            : Result<DocumentDetails>.Success(document);
    }
}
