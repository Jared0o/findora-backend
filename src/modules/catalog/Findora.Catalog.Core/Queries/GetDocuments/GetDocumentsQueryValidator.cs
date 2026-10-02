using FluentValidation;

namespace Findora.Catalog.Core.Queries.GetDocuments;

public sealed class GetDocumentsQueryValidator : AbstractValidator<GetDocumentsQuery>
{
    public GetDocumentsQueryValidator()
    {
        RuleFor(query => query.CatalogId)
            .Must(value => Guid.TryParse(value, out var id) && id != Guid.Empty)
            .WithErrorCode("Catalog.InvalidId").WithMessage("Catalog id must be a non-empty GUID.")
            .OverridePropertyName("catalogId");
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1)
            .WithErrorCode("Document.InvalidPage").WithMessage("Page must be at least 1.")
            .OverridePropertyName("page");
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100)
            .WithErrorCode("Document.InvalidPageSize").WithMessage("Page size must be between 1 and 100.")
            .OverridePropertyName("pageSize");
    }
}
