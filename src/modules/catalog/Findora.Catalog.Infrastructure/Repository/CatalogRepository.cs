using Findora.Catalog.Core.Repository;
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
        var catalog = new CatalogModel(Guid.CreateVersion7(), name);
        _context.Catalogs.Add(new CatalogRecord
        {
            Id = catalog.Id,
            Name = catalog.Name,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await _context.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(catalog.Id);
    }
}
