using System.Text.Json;

namespace Findora.Catalog.Core.Commands.CreateDocument;

public sealed record CreateDocumentCommand(string CatalogId, JsonElement Document);
