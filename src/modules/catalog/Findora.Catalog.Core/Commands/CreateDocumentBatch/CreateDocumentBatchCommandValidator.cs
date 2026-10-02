using System.Text.Json;
using Findora.Catalog.Core.Validation;
using FluentValidation;

namespace Findora.Catalog.Core.Commands.CreateDocumentBatch;

public sealed class CreateDocumentBatchCommandValidator : AbstractValidator<CreateDocumentBatchCommand>
{
    public CreateDocumentBatchCommandValidator()
    {
        RuleFor(command => command.CatalogId)
            .Must(value => Guid.TryParse(value, out var id) && id != Guid.Empty)
            .WithErrorCode("Catalog.InvalidId").WithMessage("Catalog id must be a non-empty GUID.")
            .OverridePropertyName("catalogId");
        RuleFor(command => command.Documents).Cascade(CascadeMode.Stop)
            .Must(documents => documents.ValueKind == JsonValueKind.Array)
            .WithErrorCode("ExpectedArray").WithMessage("The batch must be a JSON array.")
            .Must(documents => documents.GetArrayLength() is >= 1 and <= CatalogDocumentBatchAnalyzer.MaxDocuments)
            .WithErrorCode("InvalidBatchSize").WithMessage("A batch must contain between 1 and 100 documents.")
            .OverridePropertyName("documents");
    }
}
