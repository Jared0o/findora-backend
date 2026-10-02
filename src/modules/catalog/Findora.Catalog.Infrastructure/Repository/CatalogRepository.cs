using Findora.Catalog.Core.Repository;
using Findora.Catalog.Infrastructure.Persistence.Configurations;
using Npgsql;
using Findora.Catalog.Core.Queries.GetCatalog;
using Microsoft.EntityFrameworkCore;
using Findora.Catalog.Infrastructure.Persistence;
using Findora.Catalog.Infrastructure.Persistence.Entities;
using Findora.Shared.Abstraction.Results;
using CatalogModel = Findora.Catalog.Core.Models.Catalog;

namespace Findora.Catalog.Infrastructure.Repository;

public sealed class CatalogRepository : ICatalogRepository
{
    private readonly CatalogDbContext _context;

    public CatalogRepository(CatalogDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task<Result<Guid>> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        name = name.Trim();
        var nameExists = await _context.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1 FROM catalog.catalogs
                WHERE normalized_name = lower(btrim({name}))
            ) AS "Value"
            """).SingleAsync(cancellationToken);
        if (nameExists)
        {
            return NameAlreadyExists();
        }

        var catalog = new CatalogModel(Guid.CreateVersion7(), name);
        var entry = _context.Catalogs.Add(new CatalogRecord
        {
            Id = catalog.Id,
            Name = catalog.Name,
            CreatedAt = DateTimeOffset.UtcNow
        });
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
               { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: CatalogRecordConfiguration.UniqueNameIndex })
        {
            entry.State = EntityState.Detached;
            return NameAlreadyExists();
        }
        return Result<Guid>.Success(catalog.Id);
    }

    private static Result<Guid> NameAlreadyExists() => Result<Guid>.Failure(
        new Error("Catalog.NameAlreadyExists", "A catalog with this name already exists.", "name"));

    public async Task<CatalogDetails?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var catalog = await _context.Catalogs.AsNoTracking()
            .Where(record => record.Id == id)
            .Select(record => new CatalogDetails(record.Id, record.Name, record.CreatedAt,
                record.Fields.Select(field => new CatalogFieldDetails(
                    field.Name, field.Type, field.IsArray, field.IsRequired)).ToList()))
            .SingleOrDefaultAsync(cancellationToken);

        return catalog is null ? null : catalog with
        {
            Fields = catalog.Fields.OrderBy(field => field.Name, StringComparer.Ordinal).ToArray()
        };
    }
}
