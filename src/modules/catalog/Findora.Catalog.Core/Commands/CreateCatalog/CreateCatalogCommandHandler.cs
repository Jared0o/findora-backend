using Findora.Catalog.Core.Repository;
using Findora.Shared.Abstraction.Results;
using FluentValidation;

namespace Findora.Catalog.Core.Commands.CreateCatalog;

public sealed class CreateCatalogCommandHandler
{
    private readonly ICatalogRepository _repository;
    private readonly IValidator<CreateCatalogCommand> _validator;

    public CreateCatalogCommandHandler(ICatalogRepository repository, IValidator<CreateCatalogCommand> validator)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(validator);
        _repository = repository;
        _validator = validator;
    }

    public async Task<Result<Guid>> ExecuteAsync(CreateCatalogCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<Guid>.Failure(validation.Errors.Select(error =>
                new Error(error.ErrorCode, error.ErrorMessage, error.PropertyName)));
        }

        return await _repository.CreateAsync(command.Name.Trim(), cancellationToken);
    }
}
