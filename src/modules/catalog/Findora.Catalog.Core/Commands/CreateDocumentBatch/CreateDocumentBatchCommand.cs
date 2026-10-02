using System.Text.Json;

namespace Findora.Catalog.Core.Commands.CreateDocumentBatch;

public sealed record CreateDocumentBatchCommand(string CatalogId, JsonElement Documents);
