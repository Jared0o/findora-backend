using Findora.Shared.Abstraction.Results;

namespace Findora.Catalog.Core.Repository;

public interface ICatalogRepository
{
    public Task<Result<Guid>> CreateAsync(string name, CancellationToken cancellationToken = default);
}