using FluentValidation;

namespace Findora.Catalog.Core.Queries.GetDocument;

public sealed class GetDocumentQueryValidator : AbstractValidator<GetDocumentQuery>
{
    public GetDocumentQueryValidator()
    {
        RuleFor(query => query.CatalogId)
            .Must(value => Guid.TryParse(value, out var id) && id != Guid.Empty)
            .WithErrorCode("Catalog.InvalidId").WithMessage("Catalog id must be a non-empty GUID.")
            .OverridePropertyName("catalogId");
        RuleFor(query => query.DocumentId)
            .Must(value => Guid.TryParse(value, out var id) && id != Guid.Empty)
            .WithErrorCode("Document.InvalidId").WithMessage("Document id must be a non-empty GUID.")
            .OverridePropertyName("documentId");
    }
}
