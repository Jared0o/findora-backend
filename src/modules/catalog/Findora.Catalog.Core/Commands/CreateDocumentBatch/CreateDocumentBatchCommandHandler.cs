using Findora.Catalog.Core.Repository;
using Findora.Shared.Abstraction.Results;
using FluentValidation;

namespace Findora.Catalog.Core.Commands.CreateDocumentBatch;

public sealed class CreateDocumentBatchCommandHandler
{
    private readonly ICatalogDocumentRepository _repository;
    private readonly IValidator<CreateDocumentBatchCommand> _validator;

    public CreateDocumentBatchCommandHandler(ICatalogDocumentRepository repository, IValidator<CreateDocumentBatchCommand> validator)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(validator);
        _repository = repository;
        _validator = validator;
    }

    public async Task<Result<IReadOnlyList<Guid>>> ExecuteAsync(CreateDocumentBatchCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<IReadOnlyList<Guid>>.Failure(validation.Errors.Select(error => new Error(error.ErrorCode, error.ErrorMessage, error.PropertyName)));
        return await _repository.CreateBatchAsync(Guid.Parse(command.CatalogId), command.Documents.EnumerateArray().ToArray(), cancellationToken);
    }
}
