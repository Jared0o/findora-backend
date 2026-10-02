using FluentValidation;

namespace Findora.Catalog.Core.Commands.CreateCatalog;

public sealed class CreateCatalogCommandValidator : AbstractValidator<CreateCatalogCommand>
{
    public CreateCatalogCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .WithErrorCode("Catalog.NameRequired")
            .WithMessage("Catalog name is required.")
            .OverridePropertyName("name");
    }
}
