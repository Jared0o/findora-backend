using Findora.Shared.Abstraction.Results;
using Findora.Catalog.Core.Queries.GetCatalog;

namespace Findora.Catalog.Core.Repository;

public interface ICatalogRepository
{
    public Task<Result<Guid>> CreateAsync(string name, CancellationToken cancellationToken = default);
    Task<CatalogDetails?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}