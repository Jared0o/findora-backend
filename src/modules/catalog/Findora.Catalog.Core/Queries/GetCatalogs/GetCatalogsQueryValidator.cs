using FluentValidation;

namespace Findora.Catalog.Core.Queries.GetCatalogs;

public sealed class GetCatalogsQueryValidator : AbstractValidator<GetCatalogsQuery>
{
    public GetCatalogsQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1)
            .WithErrorCode("Catalog.InvalidPage")
            .WithMessage("Page must be at least 1.")
            .OverridePropertyName("page");
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100)
            .WithErrorCode("Catalog.InvalidPageSize")
            .WithMessage("Page size must be between 1 and 100.")
            .OverridePropertyName("pageSize");
    }
}
