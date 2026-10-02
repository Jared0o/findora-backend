using Findora.Catalog.Core.Repository;
using Findora.Shared.Abstraction.Results;
using FluentValidation;

namespace Findora.Catalog.Core.Commands.CreateDocument;

public sealed class CreateDocumentCommandHandler
{
    private readonly ICatalogDocumentRepository _repository;
    private readonly IValidator<CreateDocumentCommand> _validator;

    public CreateDocumentCommandHandler(ICatalogDocumentRepository repository, IValidator<CreateDocumentCommand> validator)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(validator);
        _repository = repository;
        _validator = validator;
    }

    public async Task<Result<Guid>> ExecuteAsync(CreateDocumentCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<Guid>.Failure(validation.Errors.Select(error => new Error(error.ErrorCode, error.ErrorMessage, error.PropertyName)));
        return await _repository.CreateAsync(Guid.Parse(command.CatalogId), command.Document, cancellationToken);
    }
}
