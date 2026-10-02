using System.Text.Json;
using FluentValidation;

namespace Findora.Catalog.Core.Commands.CreateDocument;

public sealed class CreateDocumentCommandValidator : AbstractValidator<CreateDocumentCommand>
{
    public CreateDocumentCommandValidator()
    {
        RuleFor(command => command.CatalogId)
            .Must(value => Guid.TryParse(value, out var id) && id != Guid.Empty)
            .WithErrorCode("Catalog.InvalidId").WithMessage("Catalog id must be a non-empty GUID.")
            .OverridePropertyName("catalogId");
        RuleFor(command => command.Document)
            .Cascade(CascadeMode.Stop)
            .Must(document => document.ValueKind == JsonValueKind.Object)
            .WithErrorCode("ExpectedObject").WithMessage("The document must be a JSON object.")
            .Must(document => document.EnumerateObject().Any())
            .WithErrorCode("EmptyDocument").WithMessage("The document must contain at least one field.")
            .OverridePropertyName("$");
    }
}
