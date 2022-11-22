using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Repositories;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public Task<bool> ExistsAsync(string id, string partitionKey, CancellationToken cancellationToken = default)
    {
        return _databaseRepository.ExistsAsync(
            id,
            BuildComposedPartitionKey(partitionKey),
            cancellationToken);
    }

    public Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default)
    {
        return _databaseRepository.ExistsAsync(
            IdAndPartitionKeyPrefixedPredicate(id),
            cancellationToken);
    }

    public Task<bool> ExistsAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return _databaseRepository.ExistsAsync(
            PartitionKeyPredicate.And(predicate),
            cancellationToken);
    }
}
