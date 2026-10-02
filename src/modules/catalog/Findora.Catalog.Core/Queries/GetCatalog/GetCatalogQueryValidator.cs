using FluentValidation;

namespace Findora.Catalog.Core.Queries.GetCatalog;

public sealed class GetCatalogQueryValidator : AbstractValidator<GetCatalogQuery>
{
    public GetCatalogQueryValidator()
    {
        RuleFor(query => query.Id)
            .Must(value => Guid.TryParse(value, out var id) && id != Guid.Empty)
            .WithErrorCode("Catalog.InvalidId")
            .WithMessage("Catalog id must be a non-empty GUID.")
            .OverridePropertyName("id");
    }
}
