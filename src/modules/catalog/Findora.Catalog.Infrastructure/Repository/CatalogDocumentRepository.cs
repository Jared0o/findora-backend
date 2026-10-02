using System.Data;
using System.Text.Json;
using Findora.Catalog.Core.Models;
using Findora.Catalog.Core.Repository;
using Findora.Catalog.Core.Validation;
using Findora.Catalog.Infrastructure.Persistence;
using Findora.Catalog.Infrastructure.Persistence.Entities;
using Findora.Shared.Abstraction.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using CatalogModel = Findora.Catalog.Core.Models.Catalog;

namespace Findora.Catalog.Infrastructure.Repository;

public sealed class CatalogDocumentRepository : ICatalogDocumentRepository
{
    private readonly CatalogDbContext _context;

    public CatalogDocumentRepository(CatalogDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task<Result<Guid>> CreateAsync(Guid catalogId, JsonElement document, CancellationToken cancellationToken = default)
    {
        var result = await CreateManyAsync(catalogId, [document], false, cancellationToken);
        return result.IsSuccess ? Result<Guid>.Success(result.Value[0]) : Result<Guid>.Failure(result.Errors);
    }

    public Task<Result<IReadOnlyList<Guid>>> CreateBatchAsync(Guid catalogId, IReadOnlyList<JsonElement> documents, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documents);
        cancellationToken.ThrowIfCancellationRequested();
        if (documents.Count is < 1 or > CatalogDocumentBatchAnalyzer.MaxDocuments)
            return Task.FromResult(Result<IReadOnlyList<Guid>>.Failure(
                new Error("InvalidBatchSize", "A batch must contain between 1 and 100 documents.", "documents")));
        return CreateManyAsync(catalogId, documents, true, cancellationToken);
    }

    private async Task<Result<IReadOnlyList<Guid>>> CreateManyAsync(Guid catalogId, IReadOnlyList<JsonElement> documents,
        bool isBatch, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        // Acquire the catalog lock before reading fields. Waiting writers then see the committed schema.
        var lockedIds = await _context.Database.SqlQuery<Guid>($"""
            SELECT id AS "Value" FROM catalog.catalogs WHERE id = {catalogId} FOR UPDATE
            """).ToListAsync(cancellationToken);
        if (lockedIds.Count == 0)
            return Result<IReadOnlyList<Guid>>.Failure(new Error("Catalog.NotFound", "Catalog was not found.", "catalogId"));

        var record = await _context.Catalogs.AsNoTracking().SingleAsync(record => record.Id == catalogId, cancellationToken);
        var fields = await _context.FieldDefinitions.AsNoTracking().Where(field => field.CatalogId == catalogId).ToListAsync(cancellationToken);
        var catalog = new CatalogModel(record.Id, record.Name);
        foreach (var field in fields)
            catalog.AddField(new CatalogFieldDefinition(field.Name, field.Type, field.IsArray, field.IsRequired));
        var analysis = isBatch ? CatalogDocumentBatchAnalyzer.Analyze(documents, catalog)
            : CatalogDocumentAnalyzer.Analyze(documents[0], catalog);
        if (analysis.IsFailure) return Result<IReadOnlyList<Guid>>.Failure(analysis.Errors);

        var entries = new List<EntityEntry>();
        var ids = new List<Guid>(documents.Count);
        try
        {
            foreach (var field in analysis.Value)
            {
                entries.Add(_context.FieldDefinitions.Add(new CatalogFieldRecord
                {
                    CatalogId = catalogId, Name = field.Name, Type = field.Type,
                    IsArray = field.IsArray, IsRequired = field.IsRequired
                }));
            }
            foreach (var document in documents)
            {
                var id = Guid.CreateVersion7();
                ids.Add(id);
                entries.Add(_context.Documents.Add(new DocumentRecord
                {
                    Id = id, CatalogId = catalogId, Data = document.GetRawText(), CreatedAt = DateTimeOffset.UtcNow
                }));
            }
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<IReadOnlyList<Guid>>.Success(ids.AsReadOnly());
        }
        finally
        {
            // Avoid keeping committed or rolled-back additions in a reused context.
            foreach (var entry in entries) entry.State = EntityState.Detached;
        }
    }
}
